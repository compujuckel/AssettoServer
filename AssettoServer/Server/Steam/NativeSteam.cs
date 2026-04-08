#if !DISABLE_STEAM

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Steamworks;

namespace AssettoServer.Server.Steam;

public class NativeSteam : BackgroundService, ISteam
{
    private readonly ACServerConfiguration _configuration;

    private bool _firstRun = true;
    
    public NativeSteam(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    private void Initialize()
    {
        var serverInit = new SteamServerInit("assettocorsa", "Assetto Corsa")
        {
            GamePort = _configuration.Server.UdpPort,
            Secure = true,
        }.WithQueryShareGamePort();

        try
        {
            SteamServer.Init(ISteam.AppId, serverInit);
        }
        catch
        {
            // ignored
        }

        try
        {
            SteamServer.LogOnAnonymous();
            SteamServer.OnSteamServersDisconnected += SteamServer_OnSteamServersDisconnected;
            SteamServer.OnSteamServersConnected += SteamServer_OnSteamServersConnected;
            SteamServer.OnSteamServerConnectFailure += SteamServer_OnSteamServerConnectFailure;
        }
        catch (Exception ex)
        {
            if (_firstRun) throw;
            Log.Error(ex, "Error trying to initialize SteamServer");
        }

        _firstRun = false;
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
    
    public async Task<SteamResult> ValidateSessionTicketAsync(byte[]? sessionTicket, ulong guid, ACTcpClient client)
    {
        if (sessionTicket == null) return new SteamResult { ErrorReason = "Missing session ticket" };

        var taskCompletionSource = new TaskCompletionSource<SteamResult>();
        SteamServer.OnValidateAuthTicketResponse += TicketValidateResponse;
        try
        {
            if (!SteamServer.BeginAuthSession(sessionTicket, guid))
            {
                return new SteamResult { ErrorReason = "Could not begin auth session" };
            }
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            cts.Token.Register(() => taskCompletionSource.SetCanceled(cts.Token));

            try
            {
                return await taskCompletionSource.Task;
            }
            catch (TaskCanceledException)
            {
                return new SteamResult { ErrorReason = "Verification timed out" };
            }

        }
        finally
        {
            SteamServer.OnValidateAuthTicketResponse -= TicketValidateResponse;
        }

        void TicketValidateResponse(SteamId playerSteamId, SteamId ownerSteamId, AuthResponse authResponse)
        {
            if (playerSteamId != guid)
                return;

            if (authResponse != AuthResponse.OK)
            {
                taskCompletionSource.SetResult(new SteamResult { ErrorReason = authResponse.ToString() });
                return;
            }

            client.Disconnecting += Client_OnDisconnecting;

            foreach (int appid in _configuration.Extra.ValidateDlcOwnership)
            {
                if (SteamServer.UserHasLicenseForApp(playerSteamId, appid) != UserHasLicenseForAppResult.HasLicense)
                {
                    taskCompletionSource.SetResult(new SteamResult { ErrorReason = $"Required DLC {appid} missing" });
                    return;
                }
            }
            
            taskCompletionSource.SetResult(new SteamResult
            {
                Success = true,
                SteamId = playerSteamId,
                OwnerSteamId = ownerSteamId
            });
        }
    }

    private static void Client_OnDisconnecting(ACTcpClient sender, EventArgs args)
    {
        try
        {
            SteamServer.EndSession(sender.Guid);
        }
        catch (Exception ex)
        {
            sender.Logger.Error(ex, "Error ending Steam session for client {ClientName}", sender.Name);
        }
    }

    private static void SteamServer_OnSteamServersConnected()
    {
        Log.Information("Connected to Steam Servers");
    }

    private void SteamServer_OnSteamServersDisconnected(Result result)
    {
        Log.Error("Disconnected from Steam Servers ({Reason})", result);
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

    private static void SteamServer_OnSteamServerConnectFailure(Result result, bool stillTrying)
    {
        Log.Error("Failed to connect to Steam servers ({Reason}), still trying = {StillTrying}", result, stillTrying);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Run(Initialize, stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        
        SteamServer.OnSteamServersConnected -= SteamServer_OnSteamServersConnected;
        SteamServer.OnSteamServersDisconnected -= SteamServer_OnSteamServersDisconnected;
        SteamServer.OnSteamServerConnectFailure -= SteamServer_OnSteamServerConnectFailure;
    }
}

#endif
