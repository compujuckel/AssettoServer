using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CyclePresetPlugin.Preset;

public class PresetManager : CriticalBackgroundService
{
    private readonly PresetImplementation _presetImplementation;
    private bool _presetChangeRequested = false;

    public PresetManager(PresetImplementation presetImplementation,
        IHostApplicationLifetime applicationLifetime) : base(applicationLifetime)
    {
        _presetImplementation = presetImplementation;
    }

    public PresetData CurrentPreset { get; private set; } = null!;

    public void SetTrack(PresetData preset)
    {
        CurrentPreset = preset;
        _presetChangeRequested = true;
        
        if (!CurrentPreset.IsInit)
            UpdateTrack();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_presetChangeRequested)
                    UpdateTrack();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in track service update");
            }
            finally
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private void UpdateTrack()
    {
        if (CurrentPreset.UpcomingType != null && !CurrentPreset.Type!.Equals(CurrentPreset.UpcomingType!))
        {
            Log.Information($"Track change to '{CurrentPreset.UpcomingType!.Name}' initiated");
            _presetImplementation.ChangeTrack(CurrentPreset);

            CurrentPreset.Type = CurrentPreset.UpcomingType;
            CurrentPreset.UpcomingType = null;
        }
    }
}
