using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Server.Configuration;
using Serilog;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public class SessionInfo : IOutgoingNetworkPacket
    {
        public bool IsNew;
        public byte ProtocolVersion;
        public byte SessionIndex;
        public byte CurrentSessionIndex;
        public byte SessionCount;
        public string? ServerName;  // std::wstring
        public string? Track;
        public string? TrackConfig;
        public string? Name;
        public SessionType SessionType;
        public ushort SessionTime;
        public ushort SessionLaps;
        public ushort SessionWaitTime;
        public byte AmbientTemperature;
        public byte RoadTemperature;
        public string? WeatherGraphics;
        public int ElapsedMs;  // ms to race start (might be negative if server has wait time)

        public void ToWriter(ref PacketWriter writer)
        {
            if (IsNew)
                writer.Write((byte)UdpPluginProtocol.NewSession);
            else
                writer.Write((byte)UdpPluginProtocol.SessionInfo);
            writer.Write(ProtocolVersion);
            writer.Write(SessionIndex);
            writer.Write(CurrentSessionIndex);
            writer.Write(SessionCount);
            writer.WriteString(ServerName, Encoding.UTF32);
            writer.WriteString(Track, Encoding.UTF8);
            writer.WriteString(TrackConfig, Encoding.UTF8);
            writer.WriteString(Name, Encoding.UTF8);
            writer.Write(SessionType);
            writer.Write(SessionTime);
            writer.Write(SessionLaps);
            writer.Write(SessionWaitTime);
            writer.Write(AmbientTemperature);
            writer.Write(RoadTemperature);
            writer.WriteUTF32String(WeatherGraphics);
            writer.Write(ElapsedMs);
        }
    }
}
