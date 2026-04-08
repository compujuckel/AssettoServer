using System;
using System.Threading;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.TrackParams;
using AssettoServer.Server.Weather.Implementation;
using AssettoServer.Shared.Weather;
using Microsoft.Extensions.Hosting;
using NodaTime;
using NodaTime.Extensions;
using Serilog;
using SunCalcNet;
using SunCalcNet.Model;

namespace AssettoServer.Server.Weather;

public class WeatherManager : BackgroundService, IHostedLifecycleService
{
    private readonly ACServerConfiguration _configuration;
    private readonly IWeatherImplementation _weatherImplementation;
    private readonly IWeatherTypeProvider _weatherTypeProvider;
    private readonly ITrackParamsProvider _trackParamsProvider;
    private readonly SessionManager _timeSource;
    private readonly RainHelper _rainHelper;
    private readonly CSPServerExtraOptions _cspServerExtraOptions;

    public WeatherManager(IWeatherImplementation weatherImplementation, 
        IWeatherTypeProvider weatherTypeProvider, 
        ITrackParamsProvider trackParamsProvider, 
        ACServerConfiguration configuration, 
        SessionManager timeSource, 
        RainHelper rainHelper,
        CSPServerExtraOptions cspServerExtraOptions)
    {
        _weatherImplementation = weatherImplementation;
        _weatherTypeProvider = weatherTypeProvider;
        _trackParamsProvider = trackParamsProvider;
        _configuration = configuration;
        _timeSource = timeSource;
        _rainHelper = rainHelper;
        _cspServerExtraOptions = cspServerExtraOptions;
    }

    public TrackParams.TrackParams? TrackParams { get; private set; }
    public WeatherData CurrentWeather { get; private set; } = new(new WeatherType(), new WeatherType());

    private ZonedDateTime _currentDateTime;
    public ZonedDateTime CurrentDateTime
    {
        get => _currentDateTime;
        set
        {
            _currentDateTime = value;
            UpdateSunPosition();
        }
    }

    private Instant _startDate;

    public SunPosition? CurrentSunPosition { get; private set; }

    public void SetTime(int time)
    {
        CurrentDateTime = CurrentDateTime.Date.AtStartOfDayInZone(CurrentDateTime.Zone).PlusSeconds(time);
        UpdateSunPosition();
    }
    
    public void SetWeather(WeatherData weather)
    {
        CurrentWeather = weather;
        _weatherImplementation.SendWeather(CurrentWeather, CurrentDateTime);
    }
    
    public void SetCspWeather(WeatherFxType upcoming, int duration)
    {
        Log.Information("CSP weather transitioning to {UpcomingWeatherType}", upcoming);
            
        CurrentWeather.UpcomingType = _weatherTypeProvider.GetWeatherType(upcoming);
        CurrentWeather.TransitionValue = 0;
        CurrentWeather.TransitionValueInternal = 0;
        CurrentWeather.TransitionDuration = duration * 1000;

        _weatherImplementation.SendWeather(CurrentWeather, CurrentDateTime);
    }

    public void SendWeather(ACTcpClient? client = null) => _weatherImplementation.SendWeather(CurrentWeather, CurrentDateTime, client);

    private void UpdateSunPosition()
    {
        if (TrackParams != null)
        {
            CurrentSunPosition = SunCalc.GetSunPosition(CurrentDateTime.ToDateTimeUtc(), TrackParams.Latitude, TrackParams.Longitude);
        }
    }
    
    public bool SetWeatherConfiguration(int id)
    {
        if (id < 0 || id >= _configuration.Server.Weathers.Count)
            return false;
            
        var weatherConfiguration = _configuration.Server.Weathers[id];

        _startDate = weatherConfiguration.WeatherFxParams.StartDate.HasValue
            ? Instant.FromUnixTimeSeconds(weatherConfiguration.WeatherFxParams.StartDate.Value)
            : SystemClock.Instance.GetCurrentInstant();

        var startTime = weatherConfiguration.WeatherFxParams.StartTime.HasValue
            ? LocalTime.FromSecondsSinceMidnight(weatherConfiguration.WeatherFxParams.StartTime.Value)
            : CurrentDateTime.TimeOfDay;
        
        CurrentDateTime = startTime.On(_startDate.InUtc().Date).InZoneLeniently(CurrentDateTime.Zone);
        if (weatherConfiguration.WeatherFxParams.TimeMultiplier.HasValue)
        {
            _configuration.Server.TimeOfDayMultiplier = (float)weatherConfiguration.WeatherFxParams.TimeMultiplier.Value;
        }

        var weatherType = _weatherTypeProvider.GetWeatherType(weatherConfiguration.WeatherFxParams.Type);

        float ambient = GetFloatWithVariation(weatherConfiguration.BaseTemperatureAmbient, weatherConfiguration.VariationAmbient);
            
        SetWeather(new WeatherData(weatherType, weatherType)
        {
            TemperatureAmbient = ambient,
            TemperatureRoad = GetFloatWithVariation(ambient + weatherConfiguration.BaseTemperatureRoad, weatherConfiguration.VariationRoad),
            WindSpeed = GetRandomFloatInRange(weatherConfiguration.WindBaseSpeedMin, weatherConfiguration.WindBaseSpeedMax),
            WindDirection = (int) Math.Round(GetFloatWithVariation(weatherConfiguration.WindBaseDirection, weatherConfiguration.WindVariationDirection)),
            RainIntensity = weatherType.RainIntensity,
            RainWater = weatherType.RainWater,
            RainWetness = weatherType.RainWetness,
            TrackGrip = _configuration.Server.DynamicTrack.CurrentGrip
        });

        return true;
    }

    private static float GetFloatWithVariation(float baseValue, float variation)
    {
        return GetRandomFloatInRange(baseValue - variation / 2, baseValue + variation / 2);
    }

    private static float GetRandomFloatInRange(float min, float max)
    {
        return (float) (Random.Shared.NextDouble() * (max - min) + min);
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var lastTimeUpdate = _timeSource.ServerTimeMilliseconds;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        
        while (await timer.WaitForNextTickAsync(token))
        {
            try
            {
                if (_configuration.Extra.EnableRealTime)
                {
                    CurrentDateTime = SystemClock.Instance
                        .InZone(CurrentDateTime.Zone)
                        .GetCurrentZonedDateTime();
                }
                else
                {
                    CurrentDateTime += Duration.FromMilliseconds((_timeSource.ServerTimeMilliseconds - lastTimeUpdate) * _configuration.Server.TimeOfDayMultiplier);
                    
                    if (_configuration.Extra.LockServerDate)
                    {
                        var realDate = _startDate.InZone(CurrentDateTime.Zone).LocalDateTime.Date;

                        if (realDate != CurrentDateTime.Date)
                        {
                            CurrentDateTime = realDate
                                .AtStartOfDayInZone(CurrentDateTime.Zone)
                                .PlusTicks(CurrentDateTime.TickOfDay);
                        }
                    }
                }
                
                _rainHelper.Update(CurrentWeather, _configuration.Server.DynamicTrack.CurrentGrip, _configuration.Extra.RainTrackGripReductionPercent, _timeSource.ServerTimeMilliseconds - lastTimeUpdate);
                _weatherImplementation.SendWeather(CurrentWeather, CurrentDateTime);
                lastTimeUpdate = _timeSource.ServerTimeMilliseconds;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in weather service update");
            }
        }
    }

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        await _trackParamsProvider.InitializeAsync();
        TrackParams = _trackParamsProvider.GetParamsForTrack(_configuration.Server.Track);

        DateTimeZone? timeZone;
        if (TrackParams == null || string.IsNullOrEmpty(TrackParams.Timezone))
        {
            if (_configuration.Extra.IgnoreConfigurationErrors.MissingTrackParams)
            {
                Log.Warning("Using UTC as default time zone");
                timeZone = DateTimeZone.Utc;
            }
            else
            {
                throw new ConfigurationException($"No track params found for {_configuration.Server.Track}. More info: https://assettoserver.org/docs/common-configuration-errors#missing-track-params")
                {
                    HelpLink = "https://assettoserver.org/docs/common-configuration-errors#missing-track-params"
                };
            }
        }
        else
        {
            timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(TrackParams.Timezone);

            if (timeZone == null)
            {
                throw new ConfigurationException($"Invalid time zone {TrackParams.Timezone} for track {_configuration.Server.Track}. Please enter a valid time zone for your track in cfg/data_track_params.ini.");
            }
        }

        if (_configuration.Extra.ForceServerTrackParams && TrackParams != null)
        {
            _cspServerExtraOptions.ExtraOptions += $"\r\n[WEATHER_FX]\r\nTIMEZONE_ID = {TrackParams.Timezone}\r\nLATITUDE = {TrackParams.Latitude}\r\nLONGITUDE = {TrackParams.Longitude}\r\n";
        }
        
        CurrentDateTime = SystemClock.Instance
            .InZone(timeZone)
            .GetCurrentDate()
            .AtStartOfDayInZone(timeZone)
            .PlusSeconds((long)WeatherUtils.SecondsFromSunAngle(_configuration.Server.SunAngle));
        
        if (!SetWeatherConfiguration(Random.Shared.Next(_configuration.Server.Weathers.Count)))
            throw new InvalidOperationException("Could not set initial weather configuration");
    }

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
