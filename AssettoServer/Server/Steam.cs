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
            GamePort = _server.Configuration.Server.UdpPort,
            Secure = true,
        }.WithQueryShareGamePort();

        try
        {
            SteamServer.Init(244210, serverInit);
        }
        catch
        {
            // ignored
        }

        try
        {
            SteamServer.ServerName = _server.Configuration.Server.Name.Substring(0,Math.Min(_server.Configuration.Server.Name.Length, 63));
            SteamServer.MapName = _server.Configuration.Server.Track.Substring(0,Math.Min(_server.Configuration.Server.Track.Length, 31));
            SteamServer.MaxPlayers = _server.EntryCars.Length;
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
            Log.Debug("Outgoing steam packet to {Endpoint}", dstEndpoint);
            //_server.UdpServer.Send(dstEndpoint, packet.Data, 0, packet.Size); TODO
        }
    }
    
    internal async ValueTask<bool> ValidateSessionTicketAsync(byte[]? sessionTicket, string guid, ACTcpClient client)
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
                client.Logger.Information("Steam auth ticket verification failed ({AuthResponse}) for {ClientName}", authResponse, client.Name);
                taskCompletionSource.SetResult(false);
                return;
            }
            
            client.Disconnecting += (_, _) =>
            {
                try
                {
                    SteamServer.EndSession(playerSteamId);
                }
                catch (Exception ex)
                {
                    client.Logger.Error(ex, "Error ending Steam session for client {ClientName}", client.Name);
                }
            };

            if (playerSteamId != ownerSteamId && _server.IsGuidBlacklisted(ownerSteamId.ToString()))
            {
                client.Logger.Information("{ClientName} ({SteamId}) is using Steam family sharing and game owner {OwnerSteamId} is blacklisted", client.Name, playerSteamId, ownerSteamId);
                taskCompletionSource.SetResult(false);
                return;
            }

            if (_server.Configuration.Extra.ValidateDlcOwnership != null)
            {
                foreach (int appid in _server.Configuration.Extra.ValidateDlcOwnership)
                {
                    if (SteamServer.UserHasLicenseForApp(playerSteamId, appid) != UserHasLicenseForAppResult.HasLicense)
                    {
                        client.Logger.Information("{ClientName} does not own required DLC {DlcId}", client.Name, appid);
                        taskCompletionSource.SetResult(false);
                        return;
                    }
                }
            }
            
            client.Logger.Information("Steam auth ticket verification succeeded for {ClientName}", client.Name);
            taskCompletionSource.SetResult(true);
        }

        bool validated = false;

        SteamServer.OnValidateAuthTicketResponse += ticketValidateResponse;
        Task timeoutTask = Task.Delay(5000);

        if (!SteamServer.BeginAuthSession(sessionTicket, steamId))
        {
            client.Logger.Information("Steam auth ticket verification failed for {ClientName}", client.Name);
            taskCompletionSource.SetResult(false);
        }

        Task finishedTask = await Task.WhenAny(timeoutTask, taskCompletionSource.Task);

        if (finishedTask == timeoutTask)
        {
            client.Logger.Warning("Steam auth ticket verification timed out for {ClientName}", client.Name);
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
        catch
        {
            // ignored
        }

        try
        {
            SteamServer.Shutdown();
        }
        catch
        {
            // ignored
        }

        Initialize();
    }

    private void SteamServer_OnSteamServerConnectFailure(Result result, bool stillTrying)
    {
        Log.Error("Failed to connect to Steam servers. Result {Result}, still trying = {StillTrying}", result, stillTrying);
    }
}
