using System.Diagnostics.CodeAnalysis;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Network.ClientMessages;

public abstract class OnlineEvent<TMessage> : IIncomingNetworkPacket, IOutgoingNetworkPacket where TMessage : OnlineEvent<TMessage>, new()
{
    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    public static readonly uint PacketType;

    public byte SessionId = 255;
    
    internal delegate void ToWriterDelegate(TMessage message, ref PacketWriter writer);
    internal delegate void FromReaderDelegate(TMessage message, PacketReader reader);

    private static readonly ToWriterDelegate ToWriterInternal;
    private static readonly FromReaderDelegate FromReaderInternal;

    static OnlineEvent()
    {
        var info = OnlineEventGenerator.ParseClientMessage(typeof(TMessage));

        PacketType = info.PacketType;
        FromReaderInternal = OnlineEventGenerator.GenerateReaderMethod<TMessage>(info);
        ToWriterInternal = OnlineEventGenerator.GenerateWriterMethod<TMessage>(info);
    }
    
    public void ToWriter(ref PacketWriter writer)
    {
        ToWriterInternal((TMessage)this, ref writer);
    }

    public void FromReader(PacketReader reader)
    {
        FromReaderInternal((TMessage)this, reader);
    }
}
