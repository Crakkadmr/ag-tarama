using System.Threading;
using System.Threading.Tasks;

namespace AgTarama.Services.Discovery.Listeners;

internal interface IListener
{
    string Name { get; }

    // Runs until token is cancelled.
    // subnetPrefix is optional context for filtering responses.
    Task StartAsync(string subnetPrefix, DeviceStore store, CancellationToken token);
}
