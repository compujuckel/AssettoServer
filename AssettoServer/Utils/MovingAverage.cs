namespace AssettoServer.Utils;

public class MovingAverage
{
    private readonly int _windowSize;
    private readonly float[] _window;

    private int _index = 0;
    private float _sum = 0;

    public MovingAverage(int windowSize)
    {
        _windowSize = windowSize;
        _window = new float[_windowSize];
    }

    public float Next(float value)
    {
        _sum = _sum - _window[_index] + value;
        _window[_index] = value;
        _index = (_index + 1) % _windowSize;

        return _sum / _windowSize;
    }
}
