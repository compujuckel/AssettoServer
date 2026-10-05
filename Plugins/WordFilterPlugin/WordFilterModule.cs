using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using Autofac;

namespace WordFilterPlugin;

public class WordFilterModule : AssettoServerModule<WordFilterConfiguration>
{
    public override object ReferenceConfiguration => new WordFilterConfiguration
    {
        ProhibitedUsernamePatterns = ["^Player$", "^RLD!$", "^Traffic \\d+"],
        BannableChatPatterns = ["nicecar", "fallout"],
        ProhibitedChatPatterns =
        [
            "^DRIFT-STRUCTION POINTS:",
            "^ACP: App not active$",
            "^D&O Racing APP:"
        ]
    };

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<WordFilter>().As<IOpenSlotFilter>().SingleInstance();
    }
}
