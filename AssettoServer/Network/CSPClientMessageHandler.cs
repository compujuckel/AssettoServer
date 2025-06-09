using System;
using AssettoServer.Network.ClientMessages;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Shared;
using Serilog;

namespace AssettoServer.Network;

public class CSPClientMessageHandler
{
    private readonly CSPClientMessageTypeManager _cspClientMessageTypeManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _configuration;


    public CSPClientMessageHandler(CSPClientMessageTypeManager cspClientMessageTypeManager, EntryCarManager entryCarManager,
        ACServerConfiguration configuration)
    {
        _cspClientMessageTypeManager = cspClientMessageTypeManager;
        _entryCarManager = entryCarManager;
        _configuration = configuration;
        
        cspClientMessageTypeManager.RegisterOnlineEvent<CollisionUpdatePacket>((_, _) => { });
        cspClientMessageTypeManager.RegisterOnlineEvent<TeleportCarPacket>((_, _) => { });
        cspClientMessageTypeManager.RegisterOnlineEvent<RequestResetPacket>(OnResetCar);
        cspClientMessageTypeManager.RegisterOnlineEvent<LuaReadyPacket>(OnLuaReady);
    }
    
    public void OnCSPClientMessageUdp(ACTcpClient sender, PacketReader reader)
    {
        var packetType = reader.Read<CSPClientMessageType>();
        switch (packetType)
        {
            case CSPClientMessageType.LuaMessage:
            case CSPClientMessageType.LuaMessageTargeted:
            case CSPClientMessageType.LuaMessageRanged:
            case CSPClientMessageType.LuaMessageRangedTargeted:
            {
                OnLuaMessage(sender, reader, packetType, true);
                break;
            }
            default:
            {
                if (_cspClientMessageTypeManager.RawMessageTypes.TryGetValue(packetType, out var handler))
                {
                    handler(sender, reader);
                }
                else
                {
                    CSPClientMessage clientMessage = reader.ReadPacket<CSPClientMessage>();
                    clientMessage.Type = packetType;
                    clientMessage.SessionId = sender.SessionId;
                    clientMessage.Udp = true;

                    if (_configuration.Extra.DebugClientMessages)
                    {
                        Log.Verbose("UDP client message received from {ClientName} ({SessionId}), type {Type}, data {Data}",
                            sender.Name, sender.SessionId, packetType, clientMessage.Data);
                    }

                    _entryCarManager.BroadcastPacketUdp(in clientMessage);
                }

                break;
            }
        }
    }
    
    public void OnCSPClientMessageTcp(ACTcpClient sender, PacketReader reader)
    {
        var packetType = reader.Read<CSPClientMessageType>();
        
        switch (packetType)
        {
            case CSPClientMessageType.LuaMessage:
            case CSPClientMessageType.LuaMessageTargeted:
            {
                OnLuaMessage(sender, reader, packetType, false);
                break;
            }
            case CSPClientMessageType.HandshakeOut:
            {
                OnHandshakeOut(sender, reader);
                break;
            }
            case CSPClientMessageType.AdminPenalty:
            {
                OnAdminPenaltyOut(sender, reader);
                break;
            }
            default:
            {
                if (_cspClientMessageTypeManager.RawMessageTypes.TryGetValue(packetType, out var handler))
                {
                    handler(sender, reader);
                }
                else
                {
                    var clientMessage = reader.ReadPacket<CSPClientMessage>();
                    clientMessage.Type = packetType;
                    clientMessage.SessionId = sender.SessionId;

                    if (_configuration.Extra.DebugClientMessages)
                    {
                        sender.Logger.Verbose("Client message received from {ClientName} ({SessionId}), type {Type}, data {Data}",
                            sender.Name, sender.SessionId, packetType, clientMessage.Data);
                    }
                    
                    _entryCarManager.BroadcastPacket(clientMessage);
                }

                break;
            }
        }
    }

    private void OnLuaMessage(ACTcpClient sender, PacketReader reader, CSPClientMessageType type, bool udp)
    {
        float? range = IsRanged(type) ? (float)reader.Read<Half>() : null;
        byte? sessionId = IsTargeted(type) ? reader.Read<byte>() : null;
        uint luaPacketType = reader.Read<uint>();

        if (_cspClientMessageTypeManager.MessageTypes.TryGetValue(luaPacketType, out var handler))
        {
            handler(sender, reader);
            return;
        }

        var clientMessage = reader.ReadPacket<CSPClientMessage>();
        clientMessage.SessionId = sender.SessionId;
        clientMessage.Type = IsTargeted(type) ? CSPClientMessageType.LuaMessageTargeted : CSPClientMessageType.LuaMessage;
        clientMessage.TargetSessionId = sessionId;
        clientMessage.LuaType = luaPacketType;
        clientMessage.Udp = udp;

        if (_configuration.Extra.DebugClientMessages)
        {
            sender.Logger.Debug(
                "Unknown CSP lua client message received from {ClientName} ({SessionId}), Type={LuaType:X}, UDP={Udp}, Target={TargetSessionId}, Range={Range}, Data={Data}",
                sender.Name, sender.SessionId, clientMessage.LuaType, udp, sessionId, range, Convert.ToHexString(clientMessage.Data));
        }

        if (udp)
        {
            if (sessionId.HasValue)
            {
                var client = _entryCarManager.EntryCars[sessionId.Value].Client;
                if (client != null && (!range.HasValue || sender.EntryCar.IsInRange(client.EntryCar, range.Value)))
                {
                    client.SendPacketUdp(in clientMessage);
                }
            }
            else
            {
                _entryCarManager.BroadcastPacketUdp(in clientMessage, sender, range, false);
            }
        }
        else
        {
            if (sessionId.HasValue)
            {
                _entryCarManager.EntryCars[sessionId.Value].Client?.SendPacket(clientMessage);
            }
            else
            {
                _entryCarManager.BroadcastPacket(clientMessage);
            }
        }
    }

    private static void OnHandshakeOut(ACTcpClient sender, PacketReader reader)
    {
        var packet = reader.ReadPacket<CSPHandshakeOut>();
        sender.InputMethod = packet.InputMethod;

        sender.Logger.Information("CSP handshake received from {ClientName} ({SessionId}): Version={Version} WeatherFX={WeatherFxActive} InputMethod={InputMethod} RainFX={RainFxActive} HWID={HardwareId}", 
            sender.Name, sender.SessionId, packet.Version, packet.IsWeatherFxActive, packet.InputMethod, packet.IsRainFxActive, packet.UniqueKey);
    }

    private void OnAdminPenaltyOut(ACTcpClient sender, PacketReader reader)
    {

        if (sender.IsAdministrator)
        {
            CSPAdminPenalty packet = reader.ReadPacket<CSPAdminPenalty>();
            packet.SessionId = 255;
            
            sender.Logger.Information("CSP admin penalty received from {ClientName} ({SessionId}): User is admin", 
                sender.Name, sender.SessionId);
            
            _entryCarManager.EntryCars[packet.CarIndex].Client?.SendPacket(packet);
        }
        else
        {
            sender.Logger.Information("CSP admin penalty received from {ClientName} ({SessionId}): User is not admin", 
                sender.Name, sender.SessionId);
        }
    }

    private void OnResetCar(ACTcpClient sender, RequestResetPacket packet)
    {
        if (!_configuration.Extra.EnableCarReset) return;
        sender.EntryCar.TryResetPosition();
    }

    private void OnLuaReady(ACTcpClient sender, LuaReadyPacket packet)
    {
        sender.FireLuaReady();
    }

    private static bool IsRanged(CSPClientMessageType type)
    {
        return type is CSPClientMessageType.LuaMessageRanged or CSPClientMessageType.LuaMessageRangedTargeted;
    }

    private static bool IsTargeted(CSPClientMessageType type)
    {
        return type is CSPClientMessageType.LuaMessageTargeted or CSPClientMessageType.LuaMessageRangedTargeted;
    }
}
