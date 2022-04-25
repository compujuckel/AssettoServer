using System;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Server;

public class EntryCarManager
{
    public EntryCar[] EntryCars { get; private set; } = Array.Empty<EntryCar>();

    private readonly ACServerConfiguration _configuration;
    private readonly EntryCar.Factory _entryCarFactory;

    public EntryCarManager(ACServerConfiguration configuration, EntryCar.Factory entryCarFactory)
    {
        _configuration = configuration;
        _entryCarFactory = entryCarFactory;
    }
    
    public void BroadcastPacket<TPacket>(TPacket packet, ACTcpClient? sender = null) where TPacket : IOutgoingNetworkPacket
    {
        for (int i = 0; i < EntryCars.Length; i++)
        {
            var car = EntryCars[i];
            if (car.Client is { HasSentFirstUpdate: true } && car.Client != sender)
            {
                car.Client?.SendPacket(packet);
            }
        }
    }
        
    public void BroadcastPacketUdp<TPacket>(TPacket packet, ACTcpClient? sender = null) where TPacket : IOutgoingNetworkPacket
    {
        for (int i = 0; i < EntryCars.Length; i++)
        {
            var car = EntryCars[i];
            if (car.Client is { HasSentFirstUpdate: true, HasAssociatedUdp: true } && car.Client != sender)
            {
                car.Client?.SendPacketUdp(in packet);
            }
        }
    }

    internal void Initialize()
    {
        EntryCars = new EntryCar[Math.Min(_configuration.Server.MaxClients, _configuration.EntryList.Cars.Count)];
        Log.Information("Loaded {Count} cars", EntryCars.Length);
        for (int i = 0; i < EntryCars.Length; i++)
        {
            var entry = _configuration.EntryList.Cars[i];
            var driverOptions = CSPDriverOptions.Parse(entry.Skin);
            var aiMode = _configuration.Extra.EnableAi ? entry.AiMode : AiMode.None;

            EntryCars[i] = _entryCarFactory(entry.Model, entry.Skin, (byte)i);
            EntryCars[i].SpectatorMode = entry.SpectatorMode;
            EntryCars[i].Ballast = entry.Ballast;
            EntryCars[i].Restrictor = entry.Restrictor;
            EntryCars[i].DriverOptionsFlags = driverOptions;
            EntryCars[i].AiMode = aiMode;
            EntryCars[i].AiEnableColorChanges = driverOptions.HasFlag(DriverOptionsFlags.AllowColorChange);
            EntryCars[i].AiControlled = aiMode != AiMode.None;
            EntryCars[i].NetworkDistanceSquared = MathF.Pow(_configuration.Extra.NetworkBubbleDistance, 2);
            EntryCars[i].OutsideNetworkBubbleUpdateRateMs = 1000 / _configuration.Extra.OutsideNetworkBubbleRefreshRateHz;
        }
    }
}
