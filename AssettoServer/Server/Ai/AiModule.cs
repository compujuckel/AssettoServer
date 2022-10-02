using System.IO;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
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

        if (_configuration.Extra.EnableAi)
        {
            builder.RegisterType<AiBehavior>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
            builder.RegisterType<AiUpdater>().AsSelf().SingleInstance().AutoActivate();
            builder.RegisterType<AiSlotFilter>().As<IOpenSlotFilter>();
            
            if (_configuration.Extra.AiParams.HourlyTrafficDensity != null)
            {
                builder.RegisterType<DynamicTrafficDensity>().As<IHostedService>().SingleInstance();
            }

            if (_configuration.Extra.AiParams.MaxRandomTrafficDensity != null && _configuration.Extra.AiParams.MinRandomTrafficDensity != null)
            {
                builder.RegisterType<RandomDynamicTrafficDensity>().As<IHostedService>().SingleInstance();
            }

            string contentPath = "content";
            const string contentPathCMWorkaround = "content~tmp";
            // CM renames the content folder to content~tmp when enabling the "Disable integrity verification" checkbox. We still need to load an AI spline from there, even when checksums are disabled
            if (!Directory.Exists(contentPath) && Directory.Exists(contentPathCMWorkaround))
            {
                contentPath = contentPathCMWorkaround;
            }

            string mapAiBasePath = Path.Join(contentPath, "tracks/" + _configuration.Server.Track + "/ai/");
            TrafficMap trafficMap;
            if (File.Exists(mapAiBasePath + "traffic_map.obj"))
            {
                trafficMap = WavefrontObjParser.ParseFile(mapAiBasePath + "traffic_map.obj", _configuration.Extra.AiParams.LaneWidthMeters);
            } 
            else
            {
                var parser = new FastLaneParser(_configuration);
                trafficMap = parser.FromFiles(mapAiBasePath);
            }

            builder.RegisterInstance(trafficMap).AsSelf();
        }
    }
}
