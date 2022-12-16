using AssettoServer.Network.Packets;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;

namespace AssettoServer.Network.Http.Authentication;

public class ACClientAuthentication
{
    public ACClientAuthentication(CSPClientMessageTypeManager cspClientMessageTypeManager)
    {
        cspClientMessageTypeManager.RegisterClientMessageType(ApiKeyPacket.Id, (ACTcpClient client, ref PacketReader _) =>
        {
            client.SendPacket(new ApiKeyPacket { Key = client.ApiKey });
        });
    }
}
