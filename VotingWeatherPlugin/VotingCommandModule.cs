using AssettoServer.Commands;
using Qmmands;

namespace VotingWeatherPlugin;

public class VotingCommandModule : ACModuleBase
{
    [Command("w")]
    public void VoteWeather(int choice)
    {
        VotingWeatherPlugin.Instance?.CountVote(Context.Client, choice);
    }
}
