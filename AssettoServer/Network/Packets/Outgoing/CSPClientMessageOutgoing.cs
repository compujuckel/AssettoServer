using System;
using System.IO;

namespace AssettoServer.Network.Packets.Outgoing;

public abstract class CSPClientMessageOutgoing : IOutgoingNetworkPacket
{
    public static bool ChatEncoded = true;
        
    public byte SessionId { get; init; }
    public ushort Type { get; set; }
    public byte[] Data { get; set; }
        
    private string _encoded;

    protected abstract void ToWriter(BinaryWriter writer);
        
    public void ToWriter(ref PacketWriter writer)
    {
        if (Data == null)
        {
            using var stream = new MemoryStream();
            using var binWriter = new BinaryWriter(stream);

            binWriter.Write(Type);
            ToWriter(binWriter);
            Data = stream.ToArray();
        }

        if (ChatEncoded)
        {
            if (_encoded == null)
            {
                _encoded = "\t\t\t\t$CSP0:" + Convert.ToBase64String(Data).TrimEnd('=');
            }
                
            writer.Write<byte>(0x47);
            writer.Write(SessionId);
            writer.WriteUTF32String(_encoded);
        }
        else
        {
            writer.Write<byte>(0xAB);
            writer.Write<byte>(0x03);
            writer.Write(SessionId);
            writer.WriteBytes(Data);
        }
    }
}