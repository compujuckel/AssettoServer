using AssettoServer.Server;

namespace TrafficAIPlugin;

public class AiUpdater
{
    private readonly EntryCarManager _entryCarManager;

    public AiUpdater(EntryCarManager entryCarManager, ACServer server)
    {
        _entryCarManager = entryCarManager;
        server.Update += OnUpdate;
    }
    
    private void OnUpdate(object sender, EventArgs args)
    {
        for (var i = 0; i < _entryCarManager.EntryCars.Length; i++)
        {
            var entryCar = _entryCarManager.EntryCars[i];
            if (entryCar.AiControlled)
            {
                entryCar.AiUpdate();
            }
        }
    }
}
