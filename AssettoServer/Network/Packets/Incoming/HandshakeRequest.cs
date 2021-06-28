namespace AssettoServer.Network.Packets.Incoming
{
    public struct HandshakeRequest : IIncomingNetworkPacket
    {
        public ushort ClientVersion;
        public string Guid;
        public string Name;
        public string Team;
        public string Nation;
        public string RequestedCar;
        public string Password;
        public string Features;
        public byte[] SessionTicket;

        public void FromReader(PacketReader reader)
        {
            ClientVersion = reader.Read<ushort>();
            Guid = reader.ReadASCIIString();
            Name = reader.ReadUTF32String();
            Team = reader.ReadASCIIString();
            Nation = reader.ReadASCIIString();
            RequestedCar = reader.ReadASCIIString();
            Password = reader.ReadASCIIString();

            if (reader.Buffer.Length > reader.ReadPosition + 2)
            {
                Features = reader.ReadASCIIString(true);

                short ticketLength = reader.Read<short>();
                if(ticketLength == reader.Buffer.Length - reader.ReadPosition)
                {
                    SessionTicket = new byte[ticketLength];
                    reader.ReadBytes(SessionTicket);
                }
            }
        }
    }
}
