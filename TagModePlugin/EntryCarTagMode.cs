using System.Drawing;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using TagModePlugin.Packets;

namespace TagModePlugin;

public class EntryCarTagMode
{
    private readonly EntryCarManager _entryCarManager;
    private readonly TagModeConfiguration _configuration;
    private readonly TagModePlugin _plugin;
    private readonly EntryCar _entryCar;
    public bool IsTagged { get; private set; } = false;

    public bool IsConnected => _entryCar.Client is { HasSentFirstUpdate: true };
    public Color CurrentColor { get; private set; } = Color.Empty;

    public EntryCarTagMode(EntryCar entryCar, EntryCarManager entryCarManager, TagModeConfiguration configuration, TagModePlugin plugin)
    {
        _entryCar = entryCar;
        _entryCarManager = entryCarManager;
        _configuration = configuration;
        _plugin = plugin;
    }

    public void OnDisconnecting()
    {
        UpdateColor(_plugin.NeutralColor, true);
    }

    public void OnLuaReady()
    {
        var isSessionActive = _plugin.CurrentSession is { HasEnded: false };
        if (isSessionActive && !_configuration.EnableLateJoinRunner)
        {
            SetTagged();
        }
        else
        {
            var color = isSessionActive ? _plugin.RunnerColor : _plugin.NeutralColor;
            UpdateColor(color);
        }

        foreach (var car in _plugin.Instances.Values)
        {
            car.UpdateColor(car.CurrentColor);
        }
    }

    public void OnCollision(CollisionEventArgs args)
    {
        if (IsTagged || _plugin.CurrentSession == null || args.TargetCar == null) return;
        
        var targetCar = _plugin.CurrentSession.GetCar(args.TargetCar);

        if (!targetCar.IsTagged) return;
        
        SetTagged();
        _plugin.CurrentSession.LastCaught = _entryCar;
    }

    public void SetTagged(bool val = true)
    {
        if (_entryCar.Client == null) return;
        
        IsTagged = val;
        
        if (!IsTagged) return;
        
        UpdateColor(_plugin.TaggedColor);
        _entryCar.Client.SendChatMessage("You are now a tagger.");
        _entryCar.Logger.Information("{Player} is now a tagger", _entryCar.Client.Name);
    }

    public void UpdateColor(Color color, bool disconnect = false)
    {
        CurrentColor = color;
        var packet = new TagModeColorPacket
        {
            Color = color,
            SessionId = _entryCar.SessionId,
            Disconnect = disconnect
        };
         _entryCarManager.BroadcastPacket(packet);
    }
}
