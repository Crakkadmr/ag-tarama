using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery;

internal sealed class DeviceStore
{
    private readonly ConcurrentDictionary<string, DeviceInfo> _devices =
        new(StringComparer.Ordinal);

    public event EventHandler<DeviceInfo>? DeviceChanged;

    // Normalize IP string so "192.168.001.010" and "192.168.1.10" map to the same key.
    private static string NormalizeIp(string ip) =>
        IPAddress.TryParse(ip, out var addr) ? addr.ToString() : ip;

    public DeviceInfo GetOrAdd(string ip)
    {
        var key = NormalizeIp(ip);
        var dev = _devices.GetOrAdd(key, k => new DeviceInfo { Ip = k });
        return dev;
    }

    public void Touch(string ip)
    {
        var key = NormalizeIp(ip);
        if (_devices.TryGetValue(key, out var dev))
        {
            dev.LastSeen = DateTime.Now;
            DeviceChanged?.Invoke(this, dev);
        }
    }

    public void Upsert(DeviceInfo updated)
    {
        updated.LastSeen = DateTime.Now;
        _devices[NormalizeIp(updated.Ip)] = updated;
        DeviceChanged?.Invoke(this, updated);
    }

    public void NotifyChanged(DeviceInfo dev)
    {
        dev.LastSeen = DateTime.Now;
        DeviceChanged?.Invoke(this, dev);
    }

    public IReadOnlyList<DeviceInfo> All =>
        _devices.Values.ToList();

    public bool TryGet(string ip, out DeviceInfo? dev) =>
        _devices.TryGetValue(NormalizeIp(ip), out dev);

    public void Clear() => _devices.Clear();

    public int Count => _devices.Count;
}
