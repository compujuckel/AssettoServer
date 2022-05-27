using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using AssettoServer.Network.Packets.Shared;

namespace AssettoServer.Network.Packets.Outgoing;

public abstract class CSPClientMessageOutgoing : IOutgoingNetworkPacket
{
    public static bool ChatEncoded = true;
        
    public byte SessionId { get; init; }
    public CSPClientMessageType Type { get; set; }
    public byte[]? Data { get; set; }
    
    private string? _encoded;

    protected abstract void ToWriter(BinaryWriter writer);
    
    public void ToWriter(ref PacketWriter writer)
    {
        if (Data == null)
        {
            using var stream = new MemoryStream();
            using var binWriter = new BinaryWriter(stream);

            binWriter.Write((ushort)Type);
            ToWriter(binWriter);
            Data = stream.ToArray();
        }

        if (ChatEncoded)
        {
            _encoded ??= "\t\t\t\t$CSP0:" + Convert.ToBase64String(Data).TrimEnd('=');
                
            writer.Write((byte)ACServerProtocol.Chat);
            writer.Write(SessionId);
            writer.WriteUTF32String(_encoded);
        }
        else
        {
            writer.Write((byte)ACServerProtocol.Extended);
            writer.Write((byte)CspMessageType.ClientMessage);
            writer.Write(SessionId);
            writer.WriteBytes(Data);
        }
    }
}
