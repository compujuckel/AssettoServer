using AssettoServer.Server;

namespace RaceChallengePlugin;

internal static class EntryCarExtensions
{
    internal static EntryCarRace GetRace(this EntryCar entryCar) => RaceChallengePlugin.Instances[entryCar.SessionId];
}