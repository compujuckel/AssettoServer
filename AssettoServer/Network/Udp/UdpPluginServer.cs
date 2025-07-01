using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Commands;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Kunos;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Shared.Network.Packets.UdpPlugin;
using AssettoServer.Shared.Utils;
using Microsoft.Extensions.Hosting;
using Serilog;
using ClientEvent = AssettoServer.Shared.Network.Packets.UdpPlugin.ClientEvent;
using CarConnected = AssettoServer.Shared.Network.Packets.UdpPlugin.CarConnected;
using CarDisconnected = AssettoServer.Shared.Network.Packets.UdpPlugin.CarDisconnected;
using Error = AssettoServer.Shared.Network.Packets.UdpPlugin.Error;
using Version = AssettoServer.Shared.Network.Packets.UdpPlugin.Version;

namespace AssettoServer.Network.Udp;

public class UdpPluginServer : BackgroundService
{
    private readonly ACServerConfiguration _configuration;
    private readonly ChatService _chatService;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;
    private readonly WeatherManager _weatherManager;
    private readonly SocketAddress _inAddress;
    private readonly SocketAddress _outAddress;
    private readonly Socket _socket;
    private static ThreadLocal<byte[]> SendBuffer { get; } = new(() => new byte[1500]);
    private const byte RequiredProtocolVersion = 4;
    private ushort _realtimePosInterval = 1000;

    public UdpPluginServer(
        SessionManager sessionManager,
        WeatherManager weatherManager,
        ACServerConfiguration configuration,
        EntryCarManager entryCarManager,
        ChatService chatService)
    {
        _configuration = configuration;
        _chatService = chatService;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;
        _weatherManager = weatherManager;

        // lets check if we should enable the plugin server
        if (configuration.Server.UdpPluginAddress == null || _configuration.Server.UdpPluginLocalPort == 0)
        {
            throw new ConfigurationException("UDP_PLUGIN_ADDRESS or UDP_PLUGIN_LOCAL_PORT not set");
        }

        var addressSplit = configuration.Server.UdpPluginAddress.Split(":", StringSplitOptions.TrimEntries);
        if (addressSplit.Length != 2 || !ushort.TryParse(addressSplit[1], out ushort outPort))
        {
            throw new ConfigurationException("UDP_PLUGIN_ADDRESS is invalid, needs to be in format 0.0.0.0:10000");
        }
        var ip = IPAddress.Parse(addressSplit[0]);

        if (_configuration.Server.UdpPluginLocalPort == outPort)
        {
            throw new ConfigurationException("UDP_PLUGIN_ADDRESS port needs to be different from UDP_PLUGIN_LOCAL_PORT");
        }

        _outAddress = new IPEndPoint(ip, outPort).Serialize();
        _inAddress = new IPEndPoint(ip, _configuration.Server.UdpPluginLocalPort).Serialize();

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Log.Information("Starting UDP plugin server on port {Port}", _configuration.Server.UdpPluginLocalPort);
        
        _socket.DisableUdpIcmpExceptions();
        _socket.ReceiveTimeout = 1000;
        _socket.Bind(new IPEndPoint(IPAddress.Any, _configuration.Server.UdpPluginLocalPort));

        Task realtimeTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                foreach (EntryCar car in _entryCarManager.ConnectedCars.Values)
                {
                    if (car.Client?.HasSentFirstUpdate ?? true)
                    {
                        SendPacket(new CarUpdate
                        {
                            SessionId = car.SessionId,
                            Position = car.Status.Position,
                            Velocity = car.Status.Velocity,
                            Gear = car.Status.Gear,
                            EngineRpm = car.Status.EngineRpm,
                            NormalizedSplinePosition = car.Status.NormalizedPosition,
                        });
                    }
                }
                long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                // Log.Debug($"UdpPlugin: CarUpdate took {endTime - startTime}ms");

                await Task.Delay((int)(_realtimePosInterval - (endTime - startTime)), cancellationToken);
            }
        }, cancellationToken);

        Task receiveTask = Task.Factory.StartNew(() => ReceiveLoop(cancellationToken), TaskCreationOptions.LongRunning);
        await Task.WhenAll(realtimeTask, receiveTask);
    }

    private void ReceiveLoop(CancellationToken stoppingToken)
    {
        byte[] buffer = new byte[1500];
        var address = new SocketAddress(AddressFamily.InterNetwork);

        SendPacket(new Version{ ProtocolVersion = RequiredProtocolVersion });
        SendSessionInfo(-1, true);
        _sessionManager.SessionChanged += (_, args) =>
        {
            SendEndSession();
            SendSessionInfo((short)args.NextSession.Configuration.Id, true);
        };
        _chatService.MessageReceived += (client, args) =>
        {
            SendPacket(new Chat
            {
                SessionId = client.SessionId,
                Message = args.Message,
            });
        };
        _entryCarManager.ClientConnected += OnClientConnected;
        _entryCarManager.ClientDisconnected += OnClientDisconnected;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var bytesRead = _socket.ReceiveFrom(buffer, SocketFlags.None, address);
                if (address.IpEquals(_inAddress))
                {
                    OnReceived(buffer, bytesRead);
                }
                else
                {
                    Log.Information("Ignoring UDP Plugin packet from address {Address}", address);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut) { }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in UDP plugin receive loop");
            }
        }
    }

    private void Send(byte[] buffer, int offset, int size)
    {
        _socket.SendTo(buffer.AsSpan(offset, size), SocketFlags.None, _outAddress);
    }

    private void SendPacket<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket
    {
        try
        {
            byte[] buffer = SendBuffer.Value!;
            PacketWriter writer = new PacketWriter(buffer);
            int bytesWritten = writer.WritePacket(in packet);

            Send(buffer, 0, bytesWritten);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending {PacketName} to {Address}", typeof(TPacket).Name, _outAddress);
        }
    }

    private void OnReceived(byte[] buffer, int size)
    {
        try
        {
            PacketReader packetReader = new PacketReader(null, buffer);

            UdpPluginProtocol packetId = (UdpPluginProtocol)packetReader.Read<byte>();

            switch (packetId)
            {
                case UdpPluginProtocol.NewSession:
                case UdpPluginProtocol.CarUpdate:
                case UdpPluginProtocol.ClientEvent:
                case UdpPluginProtocol.ClientFirstUpdate:
                case UdpPluginProtocol.ClosedConnection:
                case UdpPluginProtocol.EndSession:
                case UdpPluginProtocol.NewConnection:
                case UdpPluginProtocol.CarInfo:
                case UdpPluginProtocol.Chat:
                case UdpPluginProtocol.LapCompleted:
                case UdpPluginProtocol.SessionInfo:
                {
                    Log.Warning("UdpPlugin: Received an outgoing packet with id {PacketId}", packetId);
                    break;
                }
                case UdpPluginProtocol.Version:
                {
                    SendPacket(new Version { ProtocolVersion = RequiredProtocolVersion });
                    break;
                }
                case UdpPluginProtocol.SetRealtimePosInterval:
                {
                    var packet = packetReader.ReadPacket<SetRealtimePositionInterval>();
                    _realtimePosInterval = packet.Interval;
                    Log.Information("UdpPlugin: setting realtime position interval to {Interval} ms", _realtimePosInterval);
                    break;
                }
                case UdpPluginProtocol.GetCarInfo:
                {
                    GetCarInfo packet = packetReader.ReadPacket<GetCarInfo>();
                    if (_entryCarManager.ConnectedCars.TryGetValue(packet.SessionId, out EntryCar? car))
                    {
                        SendPacket(new CarInfo
                        {
                            CarId = car.SessionId,
                            IsConnected = car.Client?.IsConnected ?? false,
                            Model = car.Model,
                            Skin = car.Skin,
                            DriverName = car.Client?.Name,
                            DriverTeam = car.Client?.Team,
                            DriverGuid = car.Client?.Guid.ToString(),
                        });
                    }
                    else
                    {
                        Log.Information("CarInfo: No car with sessionId {SessionId}", packet.SessionId);
                    }
                    break;
                }
                case UdpPluginProtocol.SendChat:
                {
                    ChatMessage message = packetReader.ReadPacket<ChatMessage>();
                    if (message.SessionId < _entryCarManager.EntryCars.Length)
                    {
                        byte destSessId = message.SessionId;
                        message.SessionId = 0xFF;
                        _entryCarManager.EntryCars[destSessId].Client?.SendPacket(message);
                    }
                    else
                    {
                        Log.Information("SendChat: No car with sessionId {SessionId}", message.SessionId);
                    }
                    break;
                }
                case UdpPluginProtocol.BroadcastChat:
                {
                    string message = packetReader.ReadUTF32String();
                    _entryCarManager.BroadcastChat(message);
                    break;
                }
                case UdpPluginProtocol.GetSessionInfo:
                {
                    SendSessionInfo(packetReader.Read<short>(), false);
                    break;
                }
                case UdpPluginProtocol.SetSessionInfo:
                {
                    SetSessionInfo sessionInfo = packetReader.ReadPacket<SetSessionInfo>();
                    if (sessionInfo.SessionIndex > 0 && sessionInfo.SessionIndex < _configuration.Sessions.Count)
                    {
                        SessionConfiguration session = _configuration.Sessions[sessionInfo.SessionIndex];
                        session.Name = sessionInfo.SessionName;
                        session.Laps = sessionInfo.Laps;
                        session.Time = sessionInfo.Time;
                        session.Type = sessionInfo.SessionType;
                        session.WaitTime = sessionInfo.WaitTime;
                        _sessionManager.SetSession(sessionInfo.SessionIndex);
                        SendSessionInfo(sessionInfo.SessionIndex, false);
                        Log.Information("UdpPlugin: session {Index} set to name {SessionName}, type {SessionType}, laps {SessionLaps}, time {SessionTime}, waitTime {SessionWaitTime}",
                            sessionInfo.SessionIndex, session.Name, session.Type, session.Laps, session.Time, session.WaitTime);
                    }
                    else
                    {
                        Log.Information("UdpPlugin: received SetSessionInfo with invalid SessionIndex {Index}", sessionInfo.SessionIndex);
                    }
                    break;
                }
                case UdpPluginProtocol.KickUser:
                {
                    byte sessionId = packetReader.Read<byte>();
                    if (_entryCarManager.ConnectedCars.TryGetValue(sessionId, out EntryCar? car))
                    {
                        _ = Task.Run(() => _entryCarManager.KickAsync(car.Client, "You have been kicked."));
                    }
                    else
                    {
                        SendPacket(new Error { Message = $"ERROR: ACSP_KICK_USER index {sessionId} out of bound" });
                    }
                    break;
                }
                case UdpPluginProtocol.NextSession:
                {
                    _sessionManager.NextSession();
                    break;
                }
                case UdpPluginProtocol.RestartSession:
                {
                    _sessionManager.RestartSession();
                    break;
                }
                case UdpPluginProtocol.AdminCommand:
                {
                    // TODO: honestly this is kinda useless since we have plugins
                    // read wstring
                    // vanilla server has these commands
                    // /help
                    // /next_session
                    // /ksns
                    // /ksrs
                    // /ballast
                    // /restrictor
                    // /ban_id
                    // /kick
                    // /kick_id
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "UdpPlugin: Error while receiving a UDP packet");
        }
    }

    private void SendEndSession()
    {
        // TODO usually includes http link to result.json
        SendPacket(new EndSession());
    }
    
    private void SendSessionInfo(short sessionId, bool isNew)
    {
        SessionState currentSession = _sessionManager.CurrentSession;
        SessionConfiguration sessionConfig;

        if (sessionId == -1)
        {
            sessionConfig = currentSession.Configuration;
        }
        else if (sessionId > 0 && sessionId < _configuration.Sessions.Count)
        {
            sessionConfig = _configuration.Sessions[sessionId];
        }
        else
        {
            SendPacket(new Error { Message = $"ERROR: ACSP_GET_SESSION_INFO out of bounds: {sessionId}" });
            return;
        }

        SendPacket(new SessionInfo
        {
            IsNew = isNew,
            ProtocolVersion = RequiredProtocolVersion,
            SessionIndex = (byte)sessionConfig.Id,
            CurrentSessionIndex = (byte)_sessionManager.CurrentSessionIndex,
            SessionCount = (byte)_configuration.Sessions.Count,
            ServerName = _configuration.Server.Name,
            Track = _configuration.Server.Track,
            TrackConfig = _configuration.Server.TrackConfig,
            Name = sessionConfig.Name,
            SessionType = sessionConfig.Type,
            SessionTime = (ushort)sessionConfig.Time,
            SessionLaps = (ushort)sessionConfig.Laps,
            SessionWaitTime = (ushort)sessionConfig.WaitTime,
            AmbientTemperature = (byte)_weatherManager.CurrentWeather.TemperatureAmbient,
            RoadTemperature = (byte)_weatherManager.CurrentWeather.TemperatureRoad,
            WeatherGraphics = _weatherManager.CurrentWeather.Type.Graphics,
            ElapsedMs = (int)(currentSession.StartTimeMilliseconds +
                currentSession.SessionTimeMilliseconds - currentSession.Configuration.WaitTime)
        });
    }

    private void OnClientFirstUpdate(ACTcpClient sender, EventArgs args)
    {
        SendPacket(new ClientFirstUpdate{ SessionId = sender.SessionId });
    }

    private void OnClientDisconnected(ACTcpClient client, EventArgs args)
    {
        SendPacket(new CarDisconnected
        {
            DriverName = client.Name,
            DriverGuid = client.Guid.ToString(),
            SessionId = client.SessionId,
            CarModel = client.EntryCar.Model,
            CarSkin = client.EntryCar.Skin,
        });
    }

    private void OnClientConnected(ACTcpClient client, EventArgs args)
    {
        client.FirstUpdateSent += OnClientFirstUpdate;
        client.LapCompleted += OnLapCompleted;
        client.Collision += OnClientEvent;
            
        SendPacket(new CarConnected
        {
            DriverName = client.Name,
            DriverGuid = client.Guid.ToString(),
            SessionId = client.SessionId,
            CarModel = client.EntryCar.Model,
            CarSkin = client.EntryCar.Skin,
        });
    }

    private void OnClientEvent(ACTcpClient sender, CollisionEventArgs args)
    {
        SendPacket(new ClientEvent
        {
            EventType = (byte)(args.TargetCar == null ? ClientEventType.CollisionWithEnv : ClientEventType.CollisionWithCar),
            SessionId = sender.SessionId,
            TargetSessionId = args.TargetCar?.SessionId,
            Speed = args.Speed,
            WorldPosition = args.Position,
            RelPosition = args.RelPosition,
        });
    }

    private void OnLapCompleted(ACTcpClient sender, LapCompletedEventArgs args)
    {
        SendPacket(args.Packet);
    }
}
