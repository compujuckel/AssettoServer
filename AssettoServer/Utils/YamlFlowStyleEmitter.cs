using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace AssettoServer.Utils;

public class YamlFlowStyleEmitter<T>(IEventEmitter next) : ChainedEventEmitter(next)
{
    public override void Emit(MappingStartEventInfo eventInfo, IEmitter emitter)
    {
        if (eventInfo.Source.Type == typeof(T))
        {
            eventInfo.Style = MappingStyle.Flow;
        }

        base.Emit(eventInfo, emitter);
    }
}
