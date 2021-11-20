using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;

namespace AssettoServer.Server.Weather
{
    public class VotingWeatherProvider : IWeatherProvider
    {
        private const int NumChoices = 3;
        private const int VoteDurationSeconds = 30;

        private readonly ACServer _server;
        private readonly List<WeatherFxType> _weathers;
        private readonly List<ACTcpClient> _alreadyVoted = new List<ACTcpClient>();
        private readonly List<WeatherChoice> _availableWeathers = new();

        private bool _votingOpen = false;

        private class WeatherChoice
        {
            public WeatherFxType Weather { get; init; }
            public int Votes { get; set; }
        }

        public VotingWeatherProvider(ACServer server)
        {
            _server = server;
            _weathers = Enum.GetValues<WeatherFxType>().Except(_server.Configuration.Extra.VotingBlacklistedWeathers).ToList();
        }

        public void CountVote(ACTcpClient client, int choice)
        {
            if (!_votingOpen)
            {
                client.SendPacket(new ChatMessage { SessionId = 255, Message = "There is no ongoing weather vote."});
                return;
            }
            
            if (choice is >= NumChoices or < 0)
            {
                client.SendPacket(new ChatMessage { SessionId = 255, Message = "Invalid choice."});
                return;
            }

            if (_alreadyVoted.Contains(client))
            {
                client.SendPacket(new ChatMessage { SessionId = 255, Message = "You voted already." });
                return;
            }

            _alreadyVoted.Add(client);

            var votedWeather = _availableWeathers[choice];
            votedWeather.Votes++;
            
            client.SendPacket(new ChatMessage { SessionId = 255, Message = $"Your vote for {votedWeather.Weather} has been counted."});
        }
        
        public async Task UpdateAsync(WeatherData last = null)
        {
            if (last == null)
            {
                var type = _server.WeatherTypeProvider.GetWeatherType(WeatherFxType.Clear);
                
                // TODO make configurable
                _server.SetWeather(new WeatherData
                {
                    Type = type,
                    UpcomingType = type,
                    TemperatureAmbient = 18,
                    TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(TimeZoneInfo.ConvertTimeFromUtc(_server.CurrentDateTime, _server.TimeZone).TimeOfDay.TotalSeconds, 18, type.TemperatureCoefficient),
                    Pressure = 1000,
                    Humidity = 70,
                    WindSpeed = 0,
                    WindDirection = 0,
                    RainIntensity = 0,
                    RainWetness = 0,
                    RainWater = 0,
                    TrackGrip = _server.Configuration.DynamicTrack.BaseGrip
                });
                
                return;
            }
            
            _availableWeathers.Clear();
            _alreadyVoted.Clear();

            var weathersLeft = new List<WeatherFxType>(_weathers);

            _server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = "Vote for next weather:" });
            for (int i = 0; i < NumChoices; i++)
            {
                var nextWeather = weathersLeft[Random.Shared.Next(weathersLeft.Count)];
                _availableWeathers.Add(new WeatherChoice { Weather = nextWeather, Votes = 0});
                weathersLeft.Remove(nextWeather);
                
                _server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = $" /w {i} - {nextWeather}" });
            }

            _votingOpen = true;
            await Task.Delay(VoteDurationSeconds * 1000);
            _votingOpen = false;

            int maxVotes = _availableWeathers.Max(w => w.Votes);
            var weathers = _availableWeathers.Where(w => w.Votes == maxVotes).Select(w => w.Weather).ToList();

            var winner = weathers[Random.Shared.Next(weathers.Count)];
            var winnerType = _server.WeatherTypeProvider.GetWeatherType(winner);
            
            _server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = $"Weather vote ended. Next weather: {winner}"});
            
            _server.SetWeather(new WeatherData
            {
                Type = last.Type,
                UpcomingType = winnerType,
                TransitionDuration = 120000.0,
                TemperatureAmbient = last.TemperatureAmbient,
                TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(TimeZoneInfo.ConvertTimeFromUtc(_server.CurrentDateTime, _server.TimeZone).TimeOfDay.TotalSeconds, last.TemperatureAmbient, winnerType.TemperatureCoefficient),
                Pressure = last.Pressure,
                Humidity = (int)(winnerType.Humidity * 100),
                WindSpeed = last.WindSpeed,
                WindDirection = last.WindDirection,
                RainIntensity = last.RainIntensity,
                RainWetness = last.RainWetness,
                RainWater = last.RainWater,
                TrackGrip = last.TrackGrip
            });
        }
    }
}