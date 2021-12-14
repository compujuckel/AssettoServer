using System;
using System.IO;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets
{
    public abstract class ChatEncodedPacket : IOutgoingNetworkPacket
    {
        public byte SessionId { get; init; }
        private string _encoded;
        
        protected abstract void ToWriter(BinaryWriter writer);
        
        public void ToWriter(ref PacketWriter writer)
        {
            if (_encoded == null)
            {
                using var stream = new MemoryStream();
                using var binWriter = new BinaryWriter(stream);
                
                ToWriter(binWriter);

                _encoded = "\t\t\t\t$CSP0:" + Convert.ToBase64String(stream.ToArray()).TrimEnd('=');
            }
            
            writer.Write<byte>(0x47);
            writer.Write(SessionId);
            writer.WriteUTF32String(_encoded);
        }
    }
}