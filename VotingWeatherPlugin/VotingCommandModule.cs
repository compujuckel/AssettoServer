using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using Qmmands;

namespace VotingWeatherPlugin;

public class VotingCommandModule : ACModuleBase
{
    private readonly VotingWeather _votingWeather;

    public VotingCommandModule(VotingWeather votingWeather)
    {
        _votingWeather = votingWeather;
    }

    [Command("w"), RequireConnectedPlayer]
    public void VoteWeather(int choice)
    {
        _votingWeather.CountVote(Context.Client!, choice);
    }
}
