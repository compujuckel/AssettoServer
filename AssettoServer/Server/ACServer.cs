using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Blacklist;
using AssettoServer.Server.GeoParams;
using AssettoServer.Server.Whitelist;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;
using AssettoServer.Utils;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;

namespace AssettoServer.Server;

public class ACServer : BackgroundService, IHostedLifecycleService
{
    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly GeoParamsManager _geoParamsManager;
    private readonly ChecksumManager _checksumManager;
    private readonly IHostApplicationLifetime _applicationLifetime;

    /// <summary>
    /// Fires on each server tick in the main loop. Don't do resource intensive / long running stuff in here!
    /// </summary>
    public event EventHandler<ACServer, EventArgs>? Update;

    public ACServer(
        ACServerConfiguration configuration,
        IBlacklistService blacklistService,
        IWhitelistService whitelistService,
        SessionManager sessionManager,
        EntryCarManager entryCarManager,
        GeoParamsManager geoParamsManager,
        ChecksumManager checksumManager,
        CSPFeatureManager cspFeatureManager,
        CSPServerScriptProvider cspServerScriptProvider,
        IHostApplicationLifetime applicationLifetime,
        AiSpline? aiSpline = null)
    {
        Log.Information("Starting server");
            
        _configuration = configuration;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _geoParamsManager = geoParamsManager;
        _checksumManager = checksumManager;
        _applicationLifetime = applicationLifetime;

        blacklistService.Changed += OnBlacklistChanged;
        whitelistService.Changed += OnWhitelistChanged;

        cspFeatureManager.Add(new CSPFeature { Name = "SPECTATING_AWARE" });
        cspFeatureManager.Add(new CSPFeature { Name = "LOWER_CLIENTS_SENDING_RATE" });
        cspFeatureManager.Add(new CSPFeature { Name = "EMOJI" });
        cspFeatureManager.Add(new CSPFeature { Name = "SLOT_INDEX" });

        if (_configuration.Extra.EnableClientMessages)
        {
            if (_configuration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_1_77)
            {
                throw new ConfigurationException("Client messages need a minimum required CSP version of 0.1.77 (1937)");
            }
            
            cspFeatureManager.Add(new CSPFeature { Name = "CLIENT_MESSAGES", Mandatory = true });
            CSPClientMessageOutgoing.ChatEncoded = false;
        }

        if (_configuration.Extra.EnableUdpClientMessages)
        {
            if (_configuration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_0)
            {
                throw new ConfigurationException("UDP Client messages need a minimum required CSP version of 0.2.0 (2651)");
            }
            
            cspFeatureManager.Add(new CSPFeature { Name = "CLIENT_UDP_MESSAGES" });
        }

        if (_configuration.Extra.EnableCustomUpdate)
        {
            cspFeatureManager.Add(new CSPFeature { Name = "CUSTOM_UPDATE" });
        }
        
        cspServerScriptProvider.AddScript(Assembly.GetExecutingAssembly().GetManifestResourceStream("AssettoServer.Server.Lua.assettoserver.lua")!, "assettoserver.lua");

        if (_configuration.Extra.EnableCarReset)
        {
            if (!_configuration.Extra.EnableClientMessages || _configuration.CSPTrackOptions.MinimumCSPVersion < CSPVersion.V0_2_8  || aiSpline == null)
            {
                throw new ConfigurationException(
                    "Reset car: Minimum required CSP version of 0.2.8 (3424); Requires enabled client messages; Requires working AI spline");
            }
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Starting HTTP server on port {HttpPort}", _configuration.Server.HttpPort);
        
        var mainThread = new Thread(() => MainLoop(stoppingToken))
        {
            Name = "MainLoop",
            Priority = ThreadPriority.AboveNormal
        };
        mainThread.Start();
        
        return Task.CompletedTask;
    }

    private void OnBlacklistChanged(IBlacklistService sender, EventArgs args)
    {
        _ = Task.Run(async () =>
        {
            foreach (var client in _entryCarManager.ConnectedCars.Values.Select(c => c.Client))
            {
                if (client != null && await sender.IsBlacklistedAsync(client.Guid))
                {
                    client.Logger.Information("{ClientName} was banned after reloading blacklist", client.Name);
                    client.SendPacket(new KickCar { SessionId = client.SessionId, Reason = KickReason.VoteBlacklisted });
                    _ = client.DisconnectAsync();
                }
            }
        });
    }
    
    private void OnWhitelistChanged(IWhitelistService sender, EventArgs args)
    {
        _ = Task.Run(async () =>
        {
            foreach (var client in _entryCarManager.ConnectedCars.Values.Select(c => c.Client))
            {
                if (client != null && !await sender.IsWhitelistedAsync(client.Guid))
                {
                    _ = _entryCarManager.KickAsync(client, "not being whitelisted");
                }
            }
        });
    }

    private void MainLoop(CancellationToken stoppingToken)
    {
        int failedUpdateLoops = 0;
        int sleepMs = 1000 / _configuration.Server.RefreshRateHz;
        long nextTick = _sessionManager.ServerTimeMilliseconds;
        Dictionary<EntryCar, List<PositionUpdateOut>> positionUpdates = new();
        foreach (var entryCar in _entryCarManager.EntryCars)
        {
            positionUpdates[entryCar] = new List<PositionUpdateOut>(_entryCarManager.EntryCars.Length);
        }

        Log.Information("Starting update loop with an update rate of {RefreshRateHz}hz", _configuration.Server.RefreshRateHz);

        var updateLoopTimer = Metrics.CreateSummary("assettoserver_acserver_updateasync", "ACServer.UpdateAsync Duration", MetricDefaults.DefaultQuantiles);

        var updateLoopLateCounter = Metrics.CreateCounter("assettoserver_acserver_updateasync_late", "Total number of milliseconds the server was running behind");
        updateLoopLateCounter.Inc(0);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (updateLoopTimer.NewTimer())
                {
                    Update?.Invoke(this, EventArgs.Empty);

                    for (int i = 0; i < _entryCarManager.EntryCars.Length; i++)
                    {
                        var fromCar = _entryCarManager.EntryCars[i];
                        var fromClient = fromCar.Client;
                        if (fromClient != null && fromClient.HasSentFirstUpdate && (_sessionManager.ServerTimeMilliseconds - fromCar.LastPingTime) > 1000)
                        {
                            fromCar.LastPingTime = _sessionManager.ServerTimeMilliseconds;
                            fromClient.SendPacketUdp(new PingUpdate((uint)fromCar.LastPingTime, fromCar.Ping));

                            if (_sessionManager.ServerTimeMilliseconds - fromCar.LastPongTime > 15000)
                            {
                                fromClient.Logger.Information("{ClientName} has not sent a ping response for over 15 seconds", fromClient.Name);
                                _ = fromClient.DisconnectAsync();
                            }
                        }

                        if (fromCar.AiControlled || fromCar.HasUpdateToSend)
                        {
                            fromCar.HasUpdateToSend = false;

                            for (int j = 0; j < _entryCarManager.EntryCars.Length; j++)
                            {
                                var toCar = _entryCarManager.EntryCars[j];
                                var toClient = toCar.Client;
                                if (toCar == fromCar 
                                    || toClient == null || !toClient.HasSentFirstUpdate || toClient.UdpEndpoint == null
                                    || !fromCar.GetPositionUpdateForCar(toCar, out var update)) continue;

                                if (toClient.SupportsCSPCustomUpdate || fromCar.AiControlled)
                                {
                                    positionUpdates[toCar].Add(update);
                                }
                                else
                                {
                                    toClient.SendPacketUdp(in update);
                                }
                            }
                        }
                    }

                    foreach (var (toCar, updates) in positionUpdates)
                    {
                        if (updates.Count == 0) continue;
                            
                        var toClient = toCar.Client;
                        if (toClient != null)
                        {
                            const int chunkSize = 20;
                            for (int i = 0; i < updates.Count; i += chunkSize)
                            {
                                if (toClient.SupportsCSPCustomUpdate)
                                {
                                    var packet = new CSPPositionUpdate(CollectionsMarshal.AsSpan(updates).Slice(i, Math.Min(chunkSize, updates.Count - i)));
                                    toClient.SendPacketUdp(in packet);
                                }
                                else
                                {
                                    var packet = new BatchedPositionUpdate((uint)(_sessionManager.ServerTimeMilliseconds - toCar.TimeOffset), toCar.Ping,
                                        CollectionsMarshal.AsSpan(updates).Slice(i, Math.Min(chunkSize, updates.Count - i)));
                                    toClient.SendPacketUdp(in packet);
                                }
                            }
                        }
                            
                        updates.Clear();
                    }
                }

                if (_entryCarManager.ConnectedCars.Count > 0)
                {
                    long tickDelta;
                    do
                    {
                        long currentTick = _sessionManager.ServerTimeMilliseconds;
                        tickDelta = nextTick - currentTick;

                        if (tickDelta > 0)
                            Thread.Sleep((int)tickDelta);
                        else if (tickDelta < -sleepMs)
                        {
                            if (tickDelta < -1000)
                                Log.Warning("Server is running {TickDelta}ms behind", -tickDelta);

                            updateLoopLateCounter.Inc(-tickDelta);
                            nextTick = 0;
                            break;
                        }
                    } while (tickDelta > 0);

                    if (nextTick == 0)
                        nextTick = _sessionManager.ServerTimeMilliseconds;

                    nextTick += sleepMs;
                }
                else
                {
                    nextTick = _sessionManager.ServerTimeMilliseconds;
                    Thread.Sleep(500);
                }

                failedUpdateLoops = 0;
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                if (failedUpdateLoops < 10)
                {
                    failedUpdateLoops++;
                    Log.Error(ex, "Something went wrong while trying to do a tick update");
                }
                else
                {
                    Log.Fatal(ex, "Cannot recover from update loop error, shutting down");
                    _applicationLifetime.StopApplication();
                }
            }
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_geoParamsManager.GeoParams.Ip))
        {
            Log.Information("Invite link: {ServerInviteLink}", $"https://acstuff.club/s/q:race/online/join?ip={_geoParamsManager.GeoParams.Ip}&httpPort={_configuration.Server.HttpPort}");
        }
        
        
        Log.Information("Server startup completed");
        return Task.CompletedTask;
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        _entryCarManager.Initialize();
        _checksumManager.Initialize();
        await _geoParamsManager.InitializeAsync();
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        Log.Information("Server shutting down");
        _entryCarManager.BroadcastChat("*** Server shutting down ***");
        
        return Task.CompletedTask;
    }
    
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
