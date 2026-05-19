using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgTarama.Services.Discovery.Models;

namespace AgTarama.Services.Discovery;

internal interface IDeviceDiscoveryEngine
{
    DeviceStore Store { get; }
    bool NpcapAvailable { get; }

    Task StartScanAsync(
        IReadOnlyList<(string Prefix, int Start, int End)> subnets,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken token);

    Task StartLiveAsync(
        IReadOnlyList<(string Prefix, int Start, int End)> subnets,
        ScanOptions options,
        CancellationToken token);
}
