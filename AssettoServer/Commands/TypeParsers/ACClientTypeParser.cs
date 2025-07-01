using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using Qmmands;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AssettoServer.Commands.TypeParsers;

public class ACClientTypeParser : TypeParser<PlayerClient>
{
    private readonly EntryCarManager _entryCarManager;

    public ACClientTypeParser(EntryCarManager entryCarManager)
    {
        _entryCarManager = entryCarManager;
    }

    public override ValueTask<TypeParserResult<PlayerClient>> ParseAsync(Parameter parameter, string value, CommandContext context)
    {
        if (int.TryParse(value, out int result)
            && _entryCarManager.ConnectedCars.TryGetValue(result, out EntryCar? car)
            && car.Client is PlayerClient carClient)
        {
            return TypeParserResult<PlayerClient>.Successful(carClient);
        }
        
        if (ulong.TryParse(value, out ulong guid)
            && _entryCarManager.ConnectedCars.FirstOrDefault(x => x.Value.Client?.Guid == guid) is { Value.Client: not null } guidCar
            && guidCar.Value.Client is PlayerClient guidCarClient)
        {
            return TypeParserResult<PlayerClient>.Successful(guidCarClient);
        }

        PlayerClient? exactMatch = null;
        PlayerClient? ignoreCaseMatch = null;
        PlayerClient? containsMatch = null;
        PlayerClient? ignoreCaseContainsMatch = null;

        if (value.StartsWith('@'))
            value = value[1..];

        foreach (EntryCar entryCar in _entryCarManager.EntryCars)
        {
            if (entryCar.Client is not PlayerClient client) continue;
            if (client.Name != null)
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

        PlayerClient? bestMatch = null;
        if (exactMatch != null)
            bestMatch = exactMatch;
        else if (ignoreCaseMatch != null)
            bestMatch = ignoreCaseMatch;
        else if (containsMatch != null)
            bestMatch = containsMatch;
        else if (ignoreCaseContainsMatch != null)
            bestMatch = ignoreCaseContainsMatch;

        if (bestMatch != null)
            return TypeParserResult<PlayerClient>.Successful(bestMatch);

        return ValueTask.FromResult(TypeParserResult<PlayerClient>.Failed("This player is not connected."));
    }
}
