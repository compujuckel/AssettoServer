using AssettoServer.Commands;
using AssettoServer.Commands.Modules;
using Qmmands;

namespace VotingTrackPlugin;

public class VotingTrackCommandModule : ACModuleBase
{
    private readonly VotingTrack _votingTrack;

    public VotingTrackCommandModule(VotingTrack votingTrack)
    {
        _votingTrack = votingTrack;
    }

    [Command("votetrack"), RequireConnectedPlayer]
    public void VoteWeather(int choice)
    {
        _votingTrack.CountVote(Context.Client!, choice);
    }
}
