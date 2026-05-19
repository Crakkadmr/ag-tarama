using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery.Probes;

internal sealed class ArpProbe : IProbe
{
    public string Name => "ARP";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        if (PcapHelper.IsNpcapAvailable)
        {
            var localIp = PcapHelper.GetLocalIpForSubnet(subnetPrefix);
            if (localIp != null)
            {
                var succeeded = await TryRunWithPcapAsync(subnetPrefix, hostStart, hostEnd, localIp, store, options, token);
                if (succeeded) return;
            }
        }
        await RunWithArpCacheAsync(subnetPrefix, store, token);
    }

    private static async Task<bool> TryRunWithPcapAsync(
        string subnet, int start, int end,
        IPAddress localIp, DeviceStore store,
        ScanOptions options, CancellationToken token)
    {
        var device = PcapHelper.GetDeviceForIp(localIp);
        if (device == null) return false;

        var localMac = PcapHelper.GetLocalMac(localIp);
        if (localMac == null) return false;

        var replies = new ConcurrentDictionary<string, PhysicalAddress>(StringComparer.Ordinal);
        try
        {
            device.Open(new SharpPcap.DeviceConfiguration { Mode = SharpPcap.DeviceModes.Promiscuous, ReadTimeout = 100 });
            device.Filter = "arp";

            device.OnPacketArrival += (_, e) =>
            {
                try
                {
                    var raw = e.GetPacket();
                    var pkt = PacketDotNet.Packet.ParsePacket(raw.LinkLayerType, raw.Data);
                    var arp = pkt.Extract<PacketDotNet.ArpPacket>();
                    if (arp?.Operation == PacketDotNet.ArpOperation.Response)
                        replies[arp.SenderProtocolAddress.ToString()] = arp.SenderHardwareAddress;
                }
                catch { }
            };
            device.StartCapture();

            using var sem = new SemaphoreSlim(64);
            int count = Math.Max(0, end - start + 1);
            var tasks = Enumerable.Range(start, count).Select(i => Task.Run(async () =>
            {
                await sem.WaitAsync(token);
                try
                {
                    var targetIp = IPAddress.Parse($"{subnet}.{i}");
                    SendArpRequest(device, localMac, localIp, targetIp);
                }
                catch { }
                finally { sem.Release(); }
            }, token));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            int wait = Math.Clamp(options.ArpTimeoutMs, 500, 3000);
            await Task.Delay(wait, token).ConfigureAwait(false);

            device.StopCapture();
        }
        catch
        {
            try { device.Close(); } catch { }
            return false;
        }
        finally
        {
            try { device.Close(); } catch { }
        }

        foreach (var (ipStr, mac) in replies)
        {
            if (token.IsCancellationRequested) break;
            if (!ipStr.StartsWith(subnet + ".", StringComparison.Ordinal)) continue;
            var normalizedMac = MacUtils.Normalize(mac.ToString());
            if (!MacUtils.IsValidUnicast(normalizedMac)) continue;
            var bilgi = store.GetOrAdd(ipStr);
            bilgi.MacAdresi = normalizedMac;
            bilgi.Online    = true;
            bilgi.KesifKaynaklari.Add("ARP");
            store.NotifyChanged(bilgi);
        }
        return true;
    }

    private static void SendArpRequest(
        SharpPcap.ILiveDevice device,
        PhysicalAddress srcMac,
        IPAddress srcIp,
        IPAddress targetIp)
    {
        var arp = new PacketDotNet.ArpPacket(
            PacketDotNet.ArpOperation.Request,
            srcMac,
            srcIp,
            PhysicalAddress.Parse("000000000000"),
            targetIp);
        var eth = new PacketDotNet.EthernetPacket(
            srcMac,
            PhysicalAddress.Parse("FFFFFFFFFFFF"),
            PacketDotNet.EthernetType.Arp)
        {
            PayloadPacket = arp,
        };
        device.SendPacket(eth.Bytes);
    }

    private static async Task RunWithArpCacheAsync(
        string subnet, DeviceStore store, CancellationToken token)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("arp", "-a")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(token).ConfigureAwait(false);
            await proc.WaitForExitAsync(token).ConfigureAwait(false);

            foreach (Match m in Regex.Matches(output,
                @"(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9A-Fa-f]{2}(?:[-:][0-9A-Fa-f]{2}){5})"))
            {
                if (token.IsCancellationRequested) break;
                var ip = m.Groups["ip"].Value;
                if (!ip.StartsWith(subnet + ".", StringComparison.Ordinal)) continue;
                var normalizedMac = MacUtils.Normalize(m.Groups["mac"].Value);
                if (!MacUtils.IsValidUnicast(normalizedMac)) continue; // skip 00:00:00:00:00:00 (invalid cache entries)
                var bilgi = store.GetOrAdd(ip);
                bilgi.MacAdresi = normalizedMac;
                bilgi.Online    = true;
                bilgi.KesifKaynaklari.Add("ARP");
                store.NotifyChanged(bilgi);
            }
        }
        catch { }
    }
}
