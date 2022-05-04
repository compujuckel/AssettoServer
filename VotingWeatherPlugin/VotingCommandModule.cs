using AssettoServer.Commands;
using Qmmands;

namespace VotingWeatherPlugin;

public class VotingCommandModule : ACModuleBase
{
    private readonly VotingWeather _votingWeather;

    public VotingCommandModule(VotingWeather votingWeather)
    {
        _votingWeather = votingWeather;
    }

    [Command("w")]
    public void VoteWeather(int choice)
    {
        _votingWeather.CountVote(Context.Client, choice);
    }
}
