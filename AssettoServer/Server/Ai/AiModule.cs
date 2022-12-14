using System.IO;
using AssettoServer.Server.Ai.Structs;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;
using Serilog;

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

            string contentPath = "content";
            const string contentPathCMWorkaround = "content~tmp";
            // CM renames the content folder to content~tmp when enabling the "Disable integrity verification" checkbox. We still need to load an AI spline from there, even when checksums are disabled
            if (!Directory.Exists(contentPath) && Directory.Exists(contentPathCMWorkaround))
            {
                contentPath = contentPathCMWorkaround;
            }

            string mapAiBasePath = Path.Join(contentPath, "tracks/" + _configuration.Server.Track + "/ai/");
            var cacheKey = AiSpline.GenerateCacheKey(mapAiBasePath);
            Log.Debug("AI cache key: {0}", cacheKey);
            Directory.CreateDirectory("cache");
            var cachePath = Path.Join("cache", cacheKey);
            if (!File.Exists(cachePath))
            {
                var parser = new FastLaneParser(_configuration);
                var aiPackage = parser.FromFiles(mapAiBasePath);
                AiSplineWriter.ToFile(aiPackage, cachePath);
            }

            var aiCache = new AiSpline(cachePath);
            builder.RegisterInstance(aiCache).AsSelf();
        }
    }
}
