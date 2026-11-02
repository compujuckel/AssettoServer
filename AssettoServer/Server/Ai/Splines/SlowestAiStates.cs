using System.Threading;

namespace AssettoServer.Server.Ai.Splines;

public class SlowestAiStates
{
    private readonly AiState?[] _aiStates;
    private readonly ReaderWriterLockSlim _lock = new();

    public SlowestAiStates(int numPoints)
    {
        _aiStates = new AiState?[numPoints];
    }

    public AiState? this[int index] => _aiStates[index];

    public void Move(AiState state, int currentPointId, int nextPointId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (currentPointId > 0 && _aiStates[currentPointId] == state)
            {
                _aiStates[currentPointId] = null;
            }

            if (nextPointId > 0)
            {
                ref var currentAiState = ref _aiStates[nextPointId];
                
                if (currentAiState != null && currentAiState.CurrentSplinePointId != nextPointId)
                {
                    currentAiState = null;
                }
            
                if (currentAiState == null || state.CurrentSpeed < currentAiState.CurrentSpeed)
                {
                    currentAiState = state;
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
