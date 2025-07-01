using AssettoServer.Server.Ai;
using AssettoServer.Shared.Model;

namespace ReplayPlugin.Data;

public class ReplayFrameState
{
    public readonly List<ValueTuple<byte, CarStatus>> PlayerCars = [];
    public readonly List<ValueTuple<byte, CarStatus>> AiCars = [];
    public readonly Dictionary<AiState, short> AiStateMapping = [];
    public readonly Dictionary<byte, List<short>> AiFrameMapping = [];

    public void Reset()
    {
        PlayerCars.Clear();
        AiCars.Clear();
        AiStateMapping.Clear();
        AiFrameMapping.Clear();
    }
}
