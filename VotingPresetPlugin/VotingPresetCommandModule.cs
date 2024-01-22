using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Commands.Contexts;
using Qmmands;

namespace VotingPresetPlugin;

public class VotingPresetCommandModule : ACModuleBase
{
    private readonly VotingPresetPlugin _votingPreset;

    public VotingPresetCommandModule(VotingPresetPlugin votingPreset)
    {
        _votingPreset = votingPreset;
    }

    [Command("votetrack", "vt", "votepreset", "vp", "presetvote", "pv"), RequireConnectedPlayer]
    public void VotePreset(int choice)
    {
        _votingPreset.CountVote((ChatCommandContext)Context, choice);
    }

    [Command("presetshow", "currentpreset", "currentrack"), RequireConnectedPlayer]
    public void GetCurrentPreset()
    {
        _votingPreset.GetPreset(Context);
    }

    [Command("presetlist", "presetget", "presets"), RequireAdmin]
    public void AdminPresetList()
    {
        _votingPreset.ListAllPresets(Context);
    }

    [Command("presetstartvote", "presetvotestart"), RequireAdmin]
    public void AdminPresetVoteStart()
    {
        _votingPreset.StartVote(Context);
    }

    [Command("presetfinishvote", "presetvotefinish"), RequireAdmin]
    public void AdminPresetVoteFinish()
    {
        _votingPreset.FinishVote(Context);
    }

    [Command("presetcancelvote", "presetvotecancel"), RequireAdmin]
    public void AdminPresetVoteCancel()
    {
        _votingPreset.CancelVote(Context);
    }

    [Command("presetextendvote", "presetvoteextend"), RequireAdmin]
    public void AdminPresetVoteExtend(int seconds)
    {
        _votingPreset.ExtendVote(Context, seconds);
    }

    [Command("presetset", "presetchange", "presetuse", "presetupdate"), RequireAdmin]
    public void AdminPresetSet(int choice)
    {
        _votingPreset.SetPreset(Context, choice);
    }

    [Command("presetrandom"), RequireAdmin]
    public void AdminPresetRandom()
    {
        _votingPreset.RandomPreset(Context);
    }
}
