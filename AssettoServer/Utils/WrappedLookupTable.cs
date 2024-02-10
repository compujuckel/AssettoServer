using System;
using System.Collections.Generic;

namespace AssettoServer.Utils;

public class WrappedLookupTable
{
    private readonly List<KeyValuePair<double, double>> _table;
    private readonly double _keyMax;

    public WrappedLookupTable(List<KeyValuePair<double, double>> table, double keyMax)
    {
        _table = new List<KeyValuePair<double, double>>(table);
        _keyMax = keyMax;
    }

    public double GetValue(double input)
    {
        if (input <= _table[0].Key)
        {
            return MathUtils.Lerp(_table[^1].Value, _table[0].Value, (input + _keyMax - _table[^1].Key) / (_table[0].Key + _keyMax - _table[^1].Key));
        }
        
        if (input >= _table[^1].Key)
        {
            return MathUtils.Lerp(_table[^1].Value, _table[0].Value, (input - _table[^1].Key) / (_table[0].Key + _keyMax - _table[^1].Key));
        }

        for (int i = 0; i < _table.Count; i++)
        {
            if (input < _table[i + 1].Key)
            {
                return MathUtils.Lerp(_table[i].Value, _table[i + 1].Value, (input - _table[i].Key) / (_table[i + 1].Key - _table[i].Key));
            }
        }

        throw new InvalidOperationException($"Could not find lookup table value for {input}");
    }
}
