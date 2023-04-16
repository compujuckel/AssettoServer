using System.Text;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Incoming;

namespace AssettoServer.Network.Rcon;

public struct AuthPacket : IIncomingNetworkPacket
{
    public string RconPassword { get; set; }
    
    public void FromReader(PacketReader reader)
    {
        RconPassword = Encoding.ASCII.GetString(reader.Buffer.Slice(reader.ReadPosition, reader.Buffer.Length - 2 - reader.ReadPosition).Span);
    }
}
