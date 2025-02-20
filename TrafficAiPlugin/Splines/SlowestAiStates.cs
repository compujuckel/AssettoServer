namespace TrafficAIPlugin.Splines;

public class SlowestAiStates
{
    private readonly AiState?[] _aiStates;
    private readonly ReaderWriterLockSlim _lock = new();

    public SlowestAiStates(int numPoints)
    {
        _aiStates = new AiState?[numPoints];
    }

    public AiState? this[int index] => _aiStates[index];

    public void Enter(int pointId, AiState state)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            ref var currentAiState = ref _aiStates[pointId];
            
            if (currentAiState != null && currentAiState.CurrentSplinePointId != pointId)
            {
                _lock.EnterWriteLock();
                try
                {
                    currentAiState = null;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            
            if (currentAiState == null || state.CurrentSpeed < currentAiState.CurrentSpeed)
            {
                _lock.EnterWriteLock();
                try
                {
                    currentAiState = state;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }
    
    public void Leave(int pointId, AiState state)
    {
        if (pointId < 0) return;
        
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_aiStates[pointId] == state)
            {
                _lock.EnterWriteLock();
                try
                {
                    _aiStates[pointId] = null;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }
}
