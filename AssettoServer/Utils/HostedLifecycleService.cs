using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Utils;

public abstract class HostedLifecycleService : IHostedLifecycleService
{
    public virtual Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
