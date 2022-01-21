using System;
using System.Net;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using Serilog;
using Steamworks;

namespace AssettoServer.Server;

internal class Steam
{
    private readonly ACServer _server;
    
    internal Steam(ACServer server)
    {
        _server = server;
    }
    
    internal void Initialize()
    {
        var serverInit = new SteamServerInit("assettocorsa", "Assetto Corsa")
        {
            GamePort = _server.Configuration.UdpPort,
            Secure = true,
        }.WithQueryShareGamePort();

        try
        {
            SteamServer.Init(244210, serverInit);
        }
        catch { }

        try
        {
            SteamServer.ServerName = _server.Configuration.Name.Substring(0,Math.Min(_server.Configuration.Name.Length, 63));
            SteamServer.MapName = _server.Configuration.Track.Substring(0,Math.Min(_server.Configuration.Track.Length, 31));
            SteamServer.MaxPlayers = _server.EntryCars.Count;
            SteamServer.LogOnAnonymous();
            SteamServer.OnSteamServersDisconnected += SteamServer_OnSteamServersDisconnected;
            SteamServer.OnSteamServersConnected += SteamServer_OnSteamServersConnected;
            SteamServer.OnSteamServerConnectFailure += SteamServer_OnSteamServerConnectFailure;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Error trying to initialize SteamServer");
        }
    }

    internal void HandleIncomingPacket(byte[] data, IPEndPoint endpoint)
    {
        SteamServer.HandleIncomingPacket(data, data.Length, endpoint.Address.IpToInt32(), (ushort)endpoint.Port);

        while (SteamServer.GetOutgoingPacket(out var packet))
        {
            var dstEndpoint = new IPEndPoint((uint)IPAddress.HostToNetworkOrder((int)packet.Address), packet.Port);
            Log.Debug("outgoing steam packet to {0}", dstEndpoint);
            _server.UdpServer.Send(dstEndpoint, packet.Data, 0, packet.Size);
        }
    }
    
    internal async ValueTask<bool> ValidateSessionTicketAsync(byte[] sessionTicket, string guid, ACTcpClient client)
    {
        if (sessionTicket == null || !ulong.TryParse(guid, out ulong steamId))
            return false;

        TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
        void ticketValidateResponse(SteamId playerSteamId, SteamId ownerSteamId, AuthResponse authResponse)
        {
            if (playerSteamId != steamId)
                return;

            if (authResponse != AuthResponse.OK)
            {
                Log.Information("Steam auth ticket verification failed ({0}) for {1}", authResponse, client.Name);
                taskCompletionSource.SetResult(false);
                return;
            }
            
            client.Disconnecting += (_, _) => SteamServer.EndSession(playerSteamId);

            if (playerSteamId != ownerSteamId && _server.IsGuidBlacklisted(ownerSteamId.ToString()))
            {
                Log.Information("{0} ({1}) is using Steam family sharing and game owner {2} is blacklisted", client.Name, playerSteamId, ownerSteamId);
                taskCompletionSource.SetResult(false);
                return;
            }

            if (_server.Configuration.Extra.ValidateDlcOwnership != null)
            {
                foreach (int appid in _server.Configuration.Extra.ValidateDlcOwnership)
                {
                    if (SteamServer.UserHasLicenseForApp(playerSteamId, appid) != UserHasLicenseForAppResult.HasLicense)
                    {
                        Log.Information("{0} does not own required DLC {1}", client.Name, appid);
                        taskCompletionSource.SetResult(false);
                        return;
                    }
                }
            }
            
            Log.Information("Steam auth ticket verification succeeded for {0}", client.Name);
            taskCompletionSource.SetResult(true);
        }

        bool validated = false;

        SteamServer.OnValidateAuthTicketResponse += ticketValidateResponse;
        Task timeoutTask = Task.Delay(5000);

        if (!SteamServer.BeginAuthSession(sessionTicket, steamId))
        {
            Log.Information("Steam auth ticket verification failed for {0}", client.Name);
            taskCompletionSource.SetResult(false);
        }

        Task finishedTask = await Task.WhenAny(timeoutTask, taskCompletionSource.Task);

        if (finishedTask == timeoutTask)
        {
            Log.Warning("Steam auth ticket verification timed out for {0}", client.Name);
        }
        else
        {
            validated = await taskCompletionSource.Task;
        }

        SteamServer.OnValidateAuthTicketResponse -= ticketValidateResponse;
        return validated;
    }

    private void SteamServer_OnSteamServersConnected()
    {
        Log.Information("Connected to Steam Servers");
    }

    private void SteamServer_OnSteamServersDisconnected(Result obj)
    {
        Log.Error("Disconnected from Steam Servers");
        SteamServer.OnSteamServersConnected -= SteamServer_OnSteamServersConnected;
        SteamServer.OnSteamServersDisconnected -= SteamServer_OnSteamServersDisconnected;
        SteamServer.OnSteamServerConnectFailure -= SteamServer_OnSteamServerConnectFailure;

        try
        {
            SteamServer.LogOff();
        }
        catch { }

        try
        {
            SteamServer.Shutdown();
        }
        catch { }

        Initialize();
    }

    private void SteamServer_OnSteamServerConnectFailure(Result result, bool stillTrying)
    {
        Log.Error("Failed to connect to Steam servers. Result {0}, still trying = {1}", result, stillTrying);
    }
}