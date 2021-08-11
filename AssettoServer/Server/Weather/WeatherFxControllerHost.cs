using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using NLua;
using NLua.Exceptions;
using SunCalcNet;

namespace AssettoServer.Server.Weather
{
    public class WeatherFxControllerHost
    {
        public LuaInterface Interface { get; }
        private Lua _lua;

        private long _lastUpdate;

        private bool _disabled;

        private const string CompatLua = 
@"function math.lerp(from, to, t)
  return from + (to - from) * t
end

math.atan2 = math.atan

function math.pow(x, y)
  return x ^ y
end

math._randomseed = math.randomseed
function math.randomseed(seed)
  math._randomseed(math.floor(seed))
end

ac = {}
ac.WeatherType = {}
";

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public class LuaInterface
        {
            public ACServer Server { get; }
            public ACTcpClient SharedMemoryWithPlayer { get; set; }

            public Dictionary<string, string> Storage { get; }

            private readonly Lua _lua;

            private string _startedMessage = "";

            public struct TrackCoordinates
            {
                public double x;
                public double y;
            }

            public struct Temperatures
            {
                public int ambient;
                public int road;
            }

            public struct WindParams
            {
                public int direction;
                public int speedFrom;
                public int speedTo;
            }

            public struct TrackState
            {
                public int sessionStart;
                public int sessionTransfer;
                public int randomness;
                public int lapGain;
            }

            public struct Vec3
            {
                public double x;
                public double y;
                public double z;
            }

            public struct TimeZoneOffset
            {
                public int @base;
                public int dst;

                public override string ToString()
                {
                    return $"{{base={@base},dst={dst}}}";
                }
            }

            public struct ConditionsSetStruct
            {
                public int currentType;
                public int upcomingType;
                public float transition;
                public float humidity;
                public float variableA;
                public float variableB;
                public float variableC;
                public Temperatures temperatures;
                public WindParams wind;
                public TrackState trackState;
                public float rainIntensity;
                public float rainWetness;
                public float rainWater;

                public override string ToString()
                {
                    return $"{nameof(currentType)}: {currentType}, {nameof(upcomingType)}: {upcomingType}, {nameof(transition)}: {transition}, {nameof(humidity)}: {humidity}, {nameof(variableA)}: {variableA}, {nameof(variableB)}: {variableB}, {nameof(variableC)}: {variableC}, {nameof(temperatures)}: {temperatures}, {nameof(wind)}: {wind}, {nameof(trackState)}: {trackState}, {nameof(rainIntensity)}: {rainIntensity}, {nameof(rainWetness)}: {rainWetness}, {nameof(rainWater)}: {rainWater}";
                }
            }

            public LuaInterface(ACServer server, Lua lua)
            {
                Server = server;
                _lua = lua;
                Storage = new Dictionary<string, string>();
            }
            
            public LuaTable CreateTable()
            {
                return (LuaTable)_lua.DoString("return {}")[0];
            }

            public string load(string key)
            {
                Server.Log.Debug("load {0}", key);
                return Storage.TryGetValue(key, out string value) ? value : "";
            }

            public void store(string key, string value)
            {
                Server.Log.Debug("store {0}={1}", key, value);
                Storage[key] = value;

                if (SharedMemoryWithPlayer != null)
                {
                    string toSend = value;
                    while (toSend.Length > 200)
                    {
                        SharedMemoryWithPlayer.SendPacket(new ChatMessage
                            {SessionId = 255, Message = $"/acstorep {key} {toSend[..200]}"});
                        toSend = toSend[200..];
                    }

                    SharedMemoryWithPlayer.SendPacket(new ChatMessage { SessionId = 255, Message = $"/acstore {key} {toSend}" });
                }
            }

            public void storeFromRemote(string key, string value, bool partial = false)
            {
                if (partial)
                {
                    Server.Log.Debug("partial (remote) {0}={1}", key, value);
                    _startedMessage += value;
                }
                else
                {
                    var result = value;
                    if (_startedMessage.Length > 0)
                    {
                        result = _startedMessage + value;
                        _startedMessage = "";
                    }

                    //result = BitConverter.ToString(Convert.FromBase64String(result));
                    
                    Server.Log.Debug("store (remote) {0}={1}", key, result);
                    Storage[key] = result;
                }
                
            }

            public void debug(string arg1, string arg2)
            {
                Server.Log.Debug("WeatherFX lua debug: {0} {1}", arg1, arg2);
            }

            public int getInputWeatherType()
            {
                return (int)Server.CurrentWeather.Type.WeatherFxType;
            }

            public Temperatures getInputTemperatures()
            {
                return new()
                {
                    ambient = (int)Math.Round(Server.CurrentWeather.TemperatureAmbient),
                    road = (int)Math.Round(Server.CurrentWeather.TemperatureRoad)
                };
            }

            public WindParams getInputWind()
            {
                return new()
                {
                    direction = Server.CurrentWeather.WindDirection,
                    speedFrom = (int) Server.CurrentWeather.WindSpeed,
                    speedTo = (int) Server.CurrentWeather.WindSpeed
                };
            }

            public TrackState getInputTrackState()
            {
                return new()
                {
                    sessionStart = (int) Server.Configuration.DynamicTrack.BaseGrip,
                    sessionTransfer = (int) Server.Configuration.DynamicTrack.GripPerLap,
                    randomness = 0, // TODO not implemented
                    lapGain = (int) Server.Configuration.DynamicTrack.TotalLapCount
                };
            }

            public long getInputDate()
            {
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            public TrackCoordinates getTrackCoordinates()
            {
                return new()
                {
                    x = Server.TrackParams.Latitude,
                    y = Server.TrackParams.Longitude
                };
            }

            public float getTimeZoneOffset()
            {
                return 0;
                
                var table = CreateTable();
                table["base"] = 0;
                table["dst"] = 0;

                //return table;
            }

            public float getTimeMultiplier()
            {
                return Server.Configuration.TimeOfDayMultiplier;
            }

            public Vec3 getSunDirection()
            {
                var sunPosition = SunCalc.GetSunPosition(DateTime.UtcNow, Server.TrackParams.Latitude,
                    Server.TrackParams.Longitude); // TODO use correct time

                return new Vec3()
                {
                    x = Math.Sin(sunPosition.Azimuth) * Math.Cos(sunPosition.Altitude) * -1.0,
                    y = Math.Sin(sunPosition.Altitude),
                    z = Math.Cos(sunPosition.Azimuth) * Math.Cos(sunPosition.Altitude)
                };
            }
            
            public Vec3 getMoonDirection()
            {
                var moonPosition = MoonCalc.GetMoonPosition(DateTime.UtcNow, Server.TrackParams.Latitude,
                    Server.TrackParams.Longitude); // TODO use correct time

                return new Vec3()
                {
                    x = Math.Sin(moonPosition.Azimuth) * Math.Cos(moonPosition.Altitude) * -1.0,
                    y = Math.Sin(moonPosition.Altitude),
                    z = Math.Cos(moonPosition.Azimuth) * Math.Cos(moonPosition.Altitude)
                };
            }

            public double getMoonFraction()
            {
                return MoonCalc.GetMoonIllumination(DateTime.UtcNow).Fraction;
            }

            public int getPatchVersionCode()
            {
                return 1614; // CSP 0.1.75
            }

            public float getDaySeconds()
            {
                return Server.CurrentDaySeconds;
            }

            public int getDayOfTheYear()
            {
                if (Server.WeatherFxStartDate.HasValue)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(Server.WeatherFxStartDate.Value).DayOfYear;
                }

                return DateTime.UtcNow.DayOfYear;
            }
            
            public ConditionsSetStruct ConditionsSet()
            {
                return new()
                {
                    currentType = (int) Server.CurrentWeather.Type.WeatherFxType,
                    upcomingType = (int) Server.CurrentWeather.Type.WeatherFxType,
                    humidity = Server.CurrentWeather.Humidity,
                    temperatures = getInputTemperatures(),
                    wind = getInputWind(),
                    trackState = getInputTrackState()
                };
            }

            public void setConditionsSet(ConditionsSetStruct conditionsSet)
            {
                Server.Log.Debug("WeatherFX update: {0}", conditionsSet);

                Server.CurrentWeather = new WeatherData()
                {
                    Humidity = (int)conditionsSet.humidity,
                    Pressure = 1000,
                    RainIntensity = conditionsSet.rainIntensity,
                    RainWater = conditionsSet.rainWater,
                    RainWetness = conditionsSet.rainWetness,
                    TemperatureAmbient = conditionsSet.temperatures.ambient,
                    TemperatureRoad = conditionsSet.temperatures.road,
                    Type = Server.WeatherTypeProvider.GetWeatherType((WeatherFxType) conditionsSet.currentType),
                    WindDirection = conditionsSet.wind.direction,
                    WindSpeed = conditionsSet.wind.speedFrom
                };

                Server.SendCurrentWeather();
            }
        }

        public WeatherFxControllerHost(ACServer server)
        {
            _lua = new Lua();
            Interface = new LuaInterface(server, _lua);

            _lua.DoString(CompatLua);

            _lua["ac.debug"] = (Action<string, string>) Interface.debug;
            _lua["ac.getInputWeatherType"] = (Func<int>) Interface.getInputWeatherType;
            _lua["ac.getInputTemperatures"] = (Func<LuaInterface.Temperatures>) Interface.getInputTemperatures;
            _lua["ac.getInputWind"] = (Func<LuaInterface.WindParams>) Interface.getInputWind;
            _lua["ac.getInputTrackState"] = (Func<LuaInterface.TrackState>) Interface.getInputTrackState;
            _lua["ac.getInputDate"] = (Func<long>) Interface.getInputDate;
            _lua["ac.ConditionsSet"] = (Func<LuaInterface.ConditionsSetStruct>) Interface.ConditionsSet;
            _lua["ac.setConditionsSet"] = (Action<LuaInterface.ConditionsSetStruct>) Interface.setConditionsSet;
            _lua["ac.getTrackCoordinates"] = (Func<LuaInterface.TrackCoordinates>) Interface.getTrackCoordinates;
            _lua["ac.getTimeZoneOffset"] = (Func<float>) Interface.getTimeZoneOffset;
            _lua["ac.getTimeMultiplier"] = (Func<float>) Interface.getTimeMultiplier;
            _lua["ac.getSunDirection"] = (Func<LuaInterface.Vec3>) Interface.getSunDirection;
            _lua["ac.getMoonDirection"] = (Func<LuaInterface.Vec3>) Interface.getMoonDirection;
            _lua["ac.getMoonFraction"] = (Func<double>) Interface.getMoonFraction;
            _lua["ac.getPatchVersionCode"] = (Func<int>) Interface.getPatchVersionCode;
            _lua["ac.load"] = (Func<string, string>) Interface.load;
            _lua["ac.store"] = (Action<string, string>) Interface.store;
            _lua["ac.getDaySeconds"] = (Func<float>) Interface.getDaySeconds;
            _lua["ac.getDayOfTheYear"] = (Func<int>) Interface.getDayOfTheYear;
            
            foreach (var type in (WeatherFxType[])Enum.GetValues(typeof(WeatherFxType)))
            {
                _lua[$"ac.WeatherType.{type}"] = (int) type;
            }
        }

        public void LoadController()
        {
            _disabled = false;

            try
            {
                _lua.DoFile("extension/weather-controllers/sol/controller.lua");
            }
            catch (LuaScriptException e)
            {
                _disabled = true;
                Interface.Server.Log.Error(e, "Error loading WeatherFX controller. Controller disabled.");
            }
        }

        public void Update()
        {
            if (_disabled)
                return;

            try
            {
                long now = Environment.TickCount64;
                var update = _lua["update"] as LuaFunction;
                update.Call((now - _lastUpdate) / 1000.0);
                _lastUpdate = now;
            }
            catch (LuaScriptException e)
            {
                _disabled = true;
                Interface.Server.Log.Error(e, "Error updating WeatherFX controller. Controller disabled.");
            }
        }
    }
}