using System;
using System.Runtime.InteropServices;

namespace AssettoServer.Server;

public class SignalHandler
{
    private readonly PosixSignalRegistration? _reloadRegistration;

    public EventHandler<SignalHandler, EventArgs>? Reloaded;

    public SignalHandler()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _reloadRegistration = PosixSignalRegistration.Create((PosixSignal)10 /* SIGUSR1 */, OnReload);
        }
    }

    private void OnReload(PosixSignalContext context)
    {
        context.Cancel = true;
        Reloaded?.Invoke(this, EventArgs.Empty);
    }
}
