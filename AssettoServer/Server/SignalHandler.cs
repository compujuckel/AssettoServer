using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Server;

public class SignalHandler : IHostedService
{
    private PosixSignalRegistration? _reloadRegistration;

    public EventHandler<SignalHandler, EventArgs>? Reloaded;

    private void OnReload(PosixSignalContext context)
    {
        context.Cancel = true;
        Reloaded?.Invoke(this, EventArgs.Empty);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _reloadRegistration = PosixSignalRegistration.Create((PosixSignal)10 /* SIGUSR1 */, OnReload);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _reloadRegistration?.Dispose();
        return Task.CompletedTask;
    }
}
