using System.Numerics;
using System.Text;
using AssettoServer.Network.ClientMessages;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Shared;

namespace AssettoServer.Tests;

[OnlineEvent(Key = "testMessage")]
public class TestOnlineEvent : OnlineEvent<TestOnlineEvent>
{
    //[ClientMessageField]
    public bool TestBool;
    
    [OnlineEventField]
    public byte TestByte;

    //[ClientMessageField(Size = 50)]
    public string TestString = null!;

    [OnlineEventField(Size = 7)]
    public int[] TestArray = null!;

    [OnlineEventField(Size = 3)]
    public long[] TestArrayLong = null!;

    //[ClientMessageField(Name = "TestVec2")]
    public Vector2 TestVec2;
    
    //[ClientMessageField(Name = "TestVec3")]
    public Vector3 TestVec3;
    
    [OnlineEventField(Name = "TestVec4")]
    public Vector4 TestVec4;

    //[ClientMessageField("TestEnum")]
    //public ACServerProtocol TestEnum;

    //public partial void ToWriter(ref PacketWriter writer);

    public void ToWriter2(ref PacketWriter writer)
    {
        writer.Write(ACServerProtocol.Extended);
        writer.Write(CSPMessageTypeTcp.ClientMessage);
        writer.Write(SessionId);
        writer.Write(CSPClientMessageType.LuaMessage);
        writer.Write(0x61ED42F0);
        writer.Write(TestByte);
    }

    public static void FromReaderStatic(TestOnlineEvent message, PacketReader reader)
    {
        message.TestArray = reader.ReadArrayFixed<int>(7).ToArray();
        message.TestArrayLong = reader.ReadArrayFixed<long>(3).ToArray();
        message.TestBool = reader.Read<bool>();
        message.TestByte = reader.Read<byte>();
        message.TestString = reader.ReadStringFixed(Encoding.UTF8, 50);
    }
    
    public static void ToWriterStatic(TestOnlineEvent message, ref PacketWriter writer)
    {
        writer.Write(ACServerProtocol.Extended);
        writer.Write((byte)CSPMessageTypeTcp.ClientMessage);
        writer.Write<byte>(message.SessionId);
        writer.Write((ushort)CSPClientMessageType.LuaMessage);
        writer.Write(PacketType);
        writer.Write(message.TestBool);
        writer.Write(message.TestByte);
        writer.WriteStringFixed(message.TestString, Encoding.UTF8, 50, false);
        writer.WriteArrayFixed<int>(message.TestArray, 7, true);
    }
}
