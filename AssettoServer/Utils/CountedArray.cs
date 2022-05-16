namespace AssettoServer.Utils;

public class CountedArray<T>
{
    public readonly T[] Array;
    public int Count { get; private set; } = 0;

    public CountedArray(int maxLength)
    {
        Array = new T[maxLength];
    }

    public void Add(T elem)
    {
        Array[Count++] = elem;
    }

    public void Clear()
    {
        Count = 0;
    }
}
