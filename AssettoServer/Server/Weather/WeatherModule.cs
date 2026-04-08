using AssettoServer.Server.Configuration;
using AssettoServer.Server.Weather.Implementation;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Server.Weather;

public class WeatherModule : Module
{
    private readonly ACServerConfiguration _configuration;

    public WeatherModule(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override void Load(ContainerBuilder builder)
    {
        if (_configuration.Extra.EnableWeatherFx)
        {
            builder.RegisterType<WeatherFxV1Implementation>().As<IWeatherImplementation>().SingleInstance();
        }
        else
        {
            builder.RegisterType<VanillaWeatherImplementation>().As<IWeatherImplementation>().SingleInstance();
        }

        builder.RegisterType<RainHelper>().AsSelf();
        builder.RegisterType<DefaultWeatherTypeProvider>().As<IWeatherTypeProvider>().SingleInstance();
        builder.RegisterType<WeatherManager>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
