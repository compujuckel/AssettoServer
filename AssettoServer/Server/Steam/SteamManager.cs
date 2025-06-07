using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Blacklist;

namespace AssettoServer.Server.Steam;

public class SteamManager
{
    private readonly ISteam _steam;
    private readonly IBlacklistService _blacklist;
    
    public SteamManager(CSPFeatureManager cspFeatureManager, ISteam steam, IBlacklistService blacklist)
    {
        _steam = steam;
        _blacklist = blacklist;
        cspFeatureManager.Add(new CSPFeature
        {
            Name = "STEAM_TICKET",
            Mandatory = true
        });
    }

    public async Task<bool> ValidateSessionTicketAsync(byte[]? sessionTicket, ulong guid, ACTcpClient client)
    {
        var result = await _steam.ValidateSessionTicketAsync(sessionTicket, guid, client);

        if (result.Success)
        {
            client.Guid = result.SteamId;
            client.OwnerGuid = result.OwnerSteamId;
            if (client.Guid != client.OwnerGuid)
            {
                if (await _blacklist.IsBlacklistedAsync(client.OwnerGuid.Value))
                {
                    client.Logger.Information("{ClientName} ({SteamId}) is using Steam family sharing and game owner {OwnerSteamId} is blacklisted", client.Name, client.Guid, client.OwnerGuid);
                    return false;
                }

                client.Logger.Information("{ClientName} ({SteamId}) is using Steam family sharing, owner {OwnerSteamId}", client.Name, client.Guid, client.OwnerGuid);
            }

            client.Logger.Information("Steam authentication succeeded for {ClientName} ({SteamId})", client.Name, client.Guid);
        }
        else
        {
            client.Logger.Warning("Steam authentication failed for {ClientName} ({SessionId}): {ErrorReason}", client.Name, client.SessionId, result.ErrorReason);
        }

        return result.Success;
    }
}
