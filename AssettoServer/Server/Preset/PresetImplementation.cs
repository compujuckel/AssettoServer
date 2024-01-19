using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Preset.Restart;
using Serilog;

namespace AssettoServer.Server.Preset;

public class PresetImplementation
{
    private readonly RestartImplementation _restartImplementation;
    private readonly ACServerConfiguration _acServerConfiguration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly ChecksumManager _checksumManager;

    public PresetImplementation(SessionManager sessionManager,
        ACServerConfiguration acServerConfiguration, EntryCarManager entryCarManager, ChecksumManager checksumManager, RestartImplementation restartImplementation)
    {
        _acServerConfiguration = acServerConfiguration;
        _entryCarManager = entryCarManager;
        _checksumManager = checksumManager;
        _restartImplementation = restartImplementation;
        _sessionManager = sessionManager;
    }

    public void ChangeTrack(PresetData preset)
    {
        // Notify about restart
        Log.Information($"Restarting server");
        _restartImplementation.InitiateRestart(preset.UpcomingType!.PresetFolder, (ushort) preset.TransitionDuration);
    }
}
