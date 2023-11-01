using AssettoServer.Network.ClientMessages;
using AssettoServer.Server;

namespace AssettoServer.Network.Http.Authentication;

public class ACClientAuthentication
{
    public ACClientAuthentication(CSPClientMessageTypeManager cspClientMessageTypeManager)
    {
        cspClientMessageTypeManager.RegisterOnlineEvent<ApiKeyPacket>((client, _) =>
        {
            client.SendPacket(new ApiKeyPacket { Key = client.ApiKey });
        });
    }
}
