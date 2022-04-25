using System.IO;
using AssettoServer.Server.Configuration;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Server.Ai;

public class AiModule : Module
{
    private readonly ACServerConfiguration _configuration;

    public AiModule(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AiState>().AsSelf();
        builder.RegisterType<AiBehavior>().AsSelf().SingleInstance().AutoActivate();
        
        if (_configuration.Extra.EnableAi)
        {
            if (_configuration.Extra.AiParams.HourlyTrafficDensity != null)
            {
                builder.RegisterType<DynamicTrafficDensity>().As<IHostedService>().SingleInstance();
            }
            
            string mapAiBasePath = "content/tracks/" + _configuration.Server.Track + "/ai/";
            TrafficMap? trafficMap;
            if (File.Exists(mapAiBasePath + "traffic_map.obj"))
            {
                trafficMap = WavefrontObjParser.ParseFile(mapAiBasePath + "traffic_map.obj", _configuration.Extra.AiParams.LaneWidthMeters);
            } 
            else
            {
                var parser = new FastLaneParser(_configuration);
                trafficMap = parser.FromFiles(mapAiBasePath);
            }

            if (trafficMap == null)
            {
                throw new ConfigurationException("AI enabled but no traffic map found");
            }

            builder.RegisterInstance(trafficMap).AsSelf();
        }
    }
}
