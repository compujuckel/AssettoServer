using System.Threading.Tasks;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server.Steam;

public interface ISteam
{
    public const int AppId = 244210;
    
    Task<SteamResult> ValidateSessionTicketAsync(byte[]? sessionTicket, ulong guid, ACTcpClient client);
}
