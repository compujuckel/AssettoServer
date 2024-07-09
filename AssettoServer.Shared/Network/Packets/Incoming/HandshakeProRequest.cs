using System.IO.Hashing;
using System.Text;

namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct HandshakeProRequest : IIncomingNetworkPacket
{
    public ushort ClientVersion;
    public ulong Guid;
    public string Name;
    public string Team;
    public string Nation;
    public string RequestedCar;
    public string Password;
    public string? Features;
    public byte[]? SessionTicket;

    public void FromReader(PacketReader reader)
    {
        ClientVersion = reader.Read<ushort>();
        reader.ReadUTF8String(); // ignored
        Name = reader.ReadUTF32String();
        Guid = Hash(Name);
        Team = reader.ReadUTF8String();
        Nation = reader.ReadUTF8String();
        RequestedCar = reader.ReadUTF8String();
        Password = reader.ReadUTF8String();

        if (reader.Buffer.Length > reader.ReadPosition + 2)
        {
            Features = reader.ReadUTF8String(true);

            if (reader.Buffer.Length > reader.ReadPosition + 2)
            {
                short ticketLength = reader.Read<short>();
                if (ticketLength == reader.Buffer.Length - reader.ReadPosition)
                {
                    SessionTicket = new byte[ticketLength];
                    reader.ReadBytes(SessionTicket);
                }
            }
        }
    }

    private ulong Hash(string input)
    {
        var hash = new XxHash64();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        hash.Append(stream);
        return hash.GetCurrentHashAsUInt64();
    }

    public HandshakeRequest Convert() => new()
    {
        ClientVersion = ClientVersion,
        Guid = Guid,
        Name = Name,
        Team = Team,
        Nation = Nation,
        RequestedCar = RequestedCar,
        Password = Password,
        Features = Features,
        SessionTicket = SessionTicket
    };
}
