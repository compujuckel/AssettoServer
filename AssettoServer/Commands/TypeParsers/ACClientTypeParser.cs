using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Qmmands;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AssettoServer.Commands.TypeParsers;

public class ACClientTypeParser : TypeParser<ACTcpClient>
{
    private readonly EntryCarManager _entryCarManager;

    public ACClientTypeParser(EntryCarManager entryCarManager)
    {
        _entryCarManager = entryCarManager;
    }

    public override ValueTask<TypeParserResult<ACTcpClient>> ParseAsync(Parameter parameter, string value, CommandContext context)
    {
        if (int.TryParse(value, out int result)
            && _entryCarManager.ConnectedCars.TryGetValue(result, out EntryCar? car)
            && car.Client != null)
        {
            return TypeParserResult<ACTcpClient>.Successful(car.Client);
        }
        
        if (ulong.TryParse(value, out ulong guid)
            && _entryCarManager.ConnectedCars.FirstOrDefault(x => x.Value.Client?.Guid == guid) is { Value.Client: not null } guidCar)
        {
            return TypeParserResult<ACTcpClient>.Successful(guidCar.Value.Client);
        }

        ACTcpClient? exactMatch = null;
        ACTcpClient? ignoreCaseMatch = null;
        ACTcpClient? containsMatch = null;
        ACTcpClient? ignoreCaseContainsMatch = null;

        if (value.StartsWith('@'))
            value = value[1..];

        foreach (EntryCar entryCar in _entryCarManager.EntryCars)
        {
            ACTcpClient? client = entryCar.Client;
            if (client != null && client.Name != null)
            {
                if (client.Name == value)
                {
                    exactMatch = client;
                    break;
                }
                else if (client.Name.Equals(value, StringComparison.OrdinalIgnoreCase))
                    ignoreCaseMatch = client;
                else if (client.Name.Contains(value) && (containsMatch == null || containsMatch.Name?.Length > client.Name.Length))
                    containsMatch = client;
                else if (client.Name.Contains(value, StringComparison.OrdinalIgnoreCase) && (ignoreCaseContainsMatch == null || ignoreCaseContainsMatch.Name?.Length > client.Name.Length))
                    ignoreCaseContainsMatch = client;
            }
        }

        ACTcpClient? bestMatch = null;
        if (exactMatch != null)
            bestMatch = exactMatch;
        else if (ignoreCaseMatch != null)
            bestMatch = ignoreCaseMatch;
        else if (containsMatch != null)
            bestMatch = containsMatch;
        else if (ignoreCaseContainsMatch != null)
            bestMatch = ignoreCaseContainsMatch;

        if (bestMatch != null)
            return TypeParserResult<ACTcpClient>.Successful(bestMatch);

        return ValueTask.FromResult(TypeParserResult<ACTcpClient>.Failed("This player is not connected."));
    }
}
