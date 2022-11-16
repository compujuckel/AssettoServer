using System.Collections.Generic;

namespace AssettoServer.Server;

public class CSPFeatureManager
{
    public IReadOnlyDictionary<string, CSPFeature> Features => _features;
    
    private readonly Dictionary<string, CSPFeature> _features = new();

    public void Add(CSPFeature feature)
    {
        _features.Add(feature.Name, feature);
    }

    public bool ValidateHandshake(List<string> features)
    {
        foreach (var serverFeature in _features.Values)
        {
            if (serverFeature.Mandatory && !features.Contains(serverFeature.Name))
            {
                return false;
            }
        }

        return true;
    }
}

public class CSPFeature
{
    /// <summary>
    /// Name as it appears in the client handshake and in Content Manager.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// When true, disconnect clients that don't advertise support for this feature in the handshake.
    /// </summary>
    public bool Mandatory { get; init; }
}
