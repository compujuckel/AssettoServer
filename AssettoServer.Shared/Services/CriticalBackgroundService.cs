using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssettoServer.Shared.Services;

public abstract class CriticalBackgroundService : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly IHostApplicationLifetime _applicationLifetime;
    
    private Task? _executingTask;
    
    protected abstract Task ExecuteAsync(CancellationToken stoppingToken);

    protected CriticalBackgroundService(IHostApplicationLifetime applicationLifetime)
    {
        _applicationLifetime = applicationLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executingTask = ExecuteAsync(_stoppingCts.Token);
        
        if (_executingTask.IsCompleted)
        {
            return _executingTask;
        }

        _executingTask.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Log.Fatal(t.Exception, "Error executing critical background service");
                _applicationLifetime.StopApplication();
            }
        }, _stoppingCts.Token);
        
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
        {
            return;
        }

        try
        {
            _stoppingCts.Cancel();
        }
        finally
        {
            _ = await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    public void Dispose()
    {
        _stoppingCts.Cancel();
    }
}
