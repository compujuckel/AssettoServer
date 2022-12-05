using AssettoServer.Network.Packets.Shared;
using AssettoServer.Server;

namespace AssettoServer.Network.Http.Authentication;

public class ACClientAuthentication
{
    public ACClientAuthentication(CSPClientMessageTypeManager cspClientMessageTypeManager)
    {
        cspClientMessageTypeManager.RegisterClientMessageType(ApiKeyPacket.Id, (client, _) =>
        {
            client.SendPacket(new ApiKeyPacket { Key = client.ApiKey });
        });
    }
}
