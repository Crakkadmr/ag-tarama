using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Probes;

internal interface IProbe
{
    string Name { get; }

    Task RunRangeAsync(
        string subnetPrefix,
        int hostStart,
        int hostEnd,
        DeviceStore store,
        ScanOptions options,
        CancellationToken token);
}
