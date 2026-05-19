using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

// IPv6 Neighbor Discovery Protocol — ICMPv6 Neighbor Solicitation to ff02::1 (all-nodes)
internal sealed class NdpProbe : IProbe
{
    public string Name => "NDP";

    public async Task RunRangeAsync(
        string subnetPrefix, int hostStart, int hostEnd,
        DeviceStore store, ScanOptions options, CancellationToken token)
    {
        try
        {
            await SweepAsync(store, token).ConfigureAwait(false);
        }
        catch { }
    }

    private static async Task SweepAsync(DeviceStore store, CancellationToken token)
    {
        // Send ICMPv6 echo request to ff02::1 (all-nodes multicast) to get responses
        using var sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Raw, ProtocolType.IcmpV6);
        sock.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive, 2);

        // ICMPv6 Echo Request to all-nodes
        var allNodes = IPAddress.Parse("ff02::1");
        var ep = new IPEndPoint(allNodes, 0);
        byte[] echo = BuildIcmpv6Echo();
        await sock.SendToAsync(echo, SocketFlags.None, ep).ConfigureAwait(false);

        sock.ReceiveTimeout = 3000;
        var buf = new byte[256];
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(3000);
        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                EndPoint remoteEp = new IPEndPoint(IPAddress.IPv6Any, 0);
                var received = await sock.ReceiveFromAsync(buf, SocketFlags.None, remoteEp)
                    .WaitAsync(cts.Token).ConfigureAwait(false);
                var srcIp = ((IPEndPoint)received.RemoteEndPoint).Address.ToString();
                if (srcIp.StartsWith("fe80::", StringComparison.OrdinalIgnoreCase) ||
                    srcIp.StartsWith("ff", StringComparison.OrdinalIgnoreCase)) continue;
                var bilgi = store.GetOrAdd(srcIp);
                bilgi.Online = true;
                bilgi.KesifKaynaklari.Add("IPv6");
                store.NotifyChanged(bilgi);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private static byte[] BuildIcmpv6Echo()
    {
        // ICMPv6 Echo Request: type=128, code=0, checksum=0 (kernel fills), id=1, seq=1
        return new byte[] { 128, 0, 0, 0, 0, 1, 0, 1 };
    }
}
