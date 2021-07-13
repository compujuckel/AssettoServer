using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Packets.Outgoing
{
    public struct CSPWeatherUpdate : IOutgoingNetworkPacket
    {
        public ulong UnixTimestamp;
        public byte WeatherType;
        public byte UpcomingWeatherType;
        public ushort TransitionValue;
        public Half TemperatureAmbient;
        public Half TemperatureRoad;
        public Half TrackGrip;
        public Half WindDirectionDeg;
        public Half WindSpeed;
        public Half Humidity;
        public Half Pressure;
        public Half RainIntensity;
        public Half RainWetness;
        public Half RainWater;

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write<byte>(0xAB);
            writer.Write<byte>(0x1);
            writer.Write(UnixTimestamp);
            writer.Write(WeatherType);
            writer.Write(UpcomingWeatherType);
            writer.Write(TransitionValue);
            writer.Write(TemperatureAmbient);
            writer.Write(TemperatureRoad);
            writer.Write(TrackGrip);
            writer.Write(WindDirectionDeg);
            writer.Write(WindSpeed);
            writer.Write(Humidity);
            writer.Write(Pressure);
            writer.Write(RainIntensity);
            writer.Write(RainWetness);
            writer.Write(RainWater);
        }

        public override string ToString()
        {
            return $"{nameof(UnixTimestamp)}: {UnixTimestamp}, {nameof(WeatherType)}: {WeatherType}, {nameof(UpcomingWeatherType)}: {UpcomingWeatherType}, {nameof(TransitionValue)}: {TransitionValue}, {nameof(TemperatureAmbient)}: {TemperatureAmbient}, {nameof(TemperatureRoad)}: {TemperatureRoad}, {nameof(TrackGrip)}: {TrackGrip}, {nameof(WindDirectionDeg)}: {WindDirectionDeg}, {nameof(WindSpeed)}: {WindSpeed}, {nameof(Humidity)}: {Humidity}, {nameof(Pressure)}: {Pressure}, {nameof(RainIntensity)}: {RainIntensity}, {nameof(RainWetness)}: {RainWetness}, {nameof(RainWater)}: {RainWater}";
        }
    }
}
