namespace ReplayPlugin;

public class EntryCarExtraDataManager
{
    public EntryCarExtraData[] Data { get; private set; } = [];

    public void Initialize(int count)
    {
        Data = new EntryCarExtraData[count];
        for (var i = 0; i < count; i++)
        {
            Data[i] = new EntryCarExtraData();
        }
    }
}
