using Autofac;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace AssettoServer.Server.Plugin;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class AssettoServerModule : Module
{
    public virtual object? ReferenceConfiguration => null;
    
    public virtual void ConfigureServices(IServiceCollection services)
    {
        
    }
}

public abstract class AssettoServerModule<TConfig> : AssettoServerModule where TConfig : new()
{
    public override object? ReferenceConfiguration => new TConfig();
}
