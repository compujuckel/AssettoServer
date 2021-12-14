using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Server.Weather;
using Serilog;

namespace VotingWeatherPlugin;

public class VotingWeather
{
    private readonly ACServer _server;
    private readonly VotingWeatherConfiguration _configuration;
    private readonly List<WeatherFxType> _weathers;
    private readonly List<ACTcpClient> _alreadyVoted = new List<ACTcpClient>();
    private readonly List<WeatherChoice> _availableWeathers = new();

    private bool _votingOpen = false;
    private long _lastVote = Environment.TickCount64;

    private class WeatherChoice
    {
        public WeatherFxType Weather { get; init; }
        public int Votes { get; set; }
    }

    public VotingWeather(ACServer server, VotingWeatherConfiguration configuration)
    {
        _server = server;
        _configuration = configuration;
        
        _weathers = Enum.GetValues<WeatherFxType>().Except(_configuration.BlacklistedWeathers).ToList();
    }

    internal async Task LoopAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(_configuration.VotingIntervalMilliseconds - _configuration.VotingDurationMilliseconds);
                await UpdateAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during voting weather update");
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }

    internal void CountVote(ACTcpClient client, int choice)
    {
        if (!_votingOpen)
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "There is no ongoing weather vote." });
            return;
        }

        if (choice >= _configuration.NumChoices || choice < 0)
        {
            client.SendPacket(new ChatMessage { SessionId = 255, Message = "Invalid choice." });
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

        client.SendPacket(new ChatMessage { SessionId = 255, Message = $"Your vote for {votedWeather.Weather} has been counted." });
    }

    public async Task UpdateAsync()
    {
        var last = _server.CurrentWeather;

        _availableWeathers.Clear();
        _alreadyVoted.Clear();

        var weathersLeft = new List<WeatherFxType>(_weathers);

        _server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = "Vote for next weather:" });
        for (int i = 0; i < _configuration.NumChoices; i++)
        {
            var nextWeather = weathersLeft[Random.Shared.Next(weathersLeft.Count)];
            _availableWeathers.Add(new WeatherChoice { Weather = nextWeather, Votes = 0 });
            weathersLeft.Remove(nextWeather);

            _server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = $" /w {i} - {nextWeather}" });
        }

        _votingOpen = true;
        await Task.Delay(_configuration.VotingDurationMilliseconds);
        _votingOpen = false;

        int maxVotes = _availableWeathers.Max(w => w.Votes);
        var weathers = _availableWeathers.Where(w => w.Votes == maxVotes).Select(w => w.Weather).ToList();

        var winner = weathers[Random.Shared.Next(weathers.Count)];
        var winnerType = _server.WeatherTypeProvider.GetWeatherType(winner);

        _server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = $"Weather vote ended. Next weather: {winner}" });

        _server.SetWeather(new WeatherData
        {
            Type = last.Type,
            UpcomingType = winnerType,
            TransitionDuration = 120000.0,
            TemperatureAmbient = last.TemperatureAmbient,
            TemperatureRoad = (float)WeatherUtils.GetRoadTemperature(TimeZoneInfo.ConvertTimeFromUtc(_server.CurrentDateTime, _server.TimeZone).TimeOfDay.TotalSeconds, last.TemperatureAmbient,
                winnerType.TemperatureCoefficient),
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