using System;
using System.Collections.Generic;

namespace AssettoServer.Utils;

public class LookupTable
{
    private readonly List<KeyValuePair<double, double>> _table;

    public LookupTable(List<KeyValuePair<double, double>> table)
    {
        _table = new List<KeyValuePair<double, double>>(table);
    }

    public double GetValue(double input)
    {
        if (input <= _table[0].Key)
        {
            return _table[0].Value;
        }

        if (input >= _table[^1].Key)
        {
            return _table[^1].Value;
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
