using System;
using System.Net;
using System.Net.NetworkInformation;

namespace AgTarama.Services.Discovery;

internal static class PcapHelper
{
    private static readonly Lazy<bool> _available = new(CheckAvailable);

    public static bool IsNpcapAvailable => _available.Value;

    private static bool CheckAvailable()
    {
        try
        {
            var _ = SharpPcap.CaptureDeviceList.Instance;
            return _.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public static SharpPcap.ILiveDevice? GetDeviceForIp(IPAddress localIp)
    {
        if (!IsNpcapAvailable) return null;
        try
        {
            foreach (var dev in SharpPcap.CaptureDeviceList.Instance)
            {
                if (dev is not SharpPcap.LibPcap.LibPcapLiveDevice live) continue;
                foreach (var addr in live.Addresses)
                {
                    if (addr.Addr?.ipAddress != null &&
                        addr.Addr.ipAddress.Equals(localIp))
                        return live;
                }
            }
        }
        catch { }
        return null;
    }

    public static PhysicalAddress? GetLocalMac(IPAddress localIp)
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.Equals(localIp))
                    return ni.GetPhysicalAddress();
            }
        }
        return null;
    }

    public static IPAddress? GetLocalIpForSubnet(string subnetPrefix)
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var uni in ni.GetIPProperties().UnicastAddresses)
            {
                var ip = uni.Address.ToString();
                if (ip.StartsWith(subnetPrefix + ".", StringComparison.Ordinal))
                    return uni.Address;
            }
        }
        return null;
    }
}
