namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class CarSetup : IOutgoingNetworkPacket
{
    public required Dictionary<string, float> Setup;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.CarSetup);
        writer.Write(true);
        writer.Write((byte)Setup.Count);
        foreach (var (name, val) in Setup)
        {
            writer.WriteUTF8String(name);
            writer.Write((float)val);
        }
    }
}
