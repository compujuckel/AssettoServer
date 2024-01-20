using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Commands.Contexts;
using Qmmands;

namespace CyclePresetPlugin;

public class CyclePresetCommandModule : ACModuleBase
{
    private readonly CyclePresetPlugin _cyclePreset;

    public CyclePresetCommandModule(CyclePresetPlugin cyclePreset)
    {
        _cyclePreset = cyclePreset;
    }

    [Command("votetrack", "vt", "votepreset", "vp", "presetvote", "pv"), RequireConnectedPlayer]
    public void VotePreset(int choice)
    {
        _cyclePreset.CountVote((ChatCommandContext)Context, choice);
    }

    [Command("presetshow", "currentpreset", "currentrack"), RequireConnectedPlayer]
    public void GetCurrentPreset()
    {
        _cyclePreset.GetPreset(Context);
    }

    [Command("presetlist", "presetget", "presets"), RequireAdmin]
    public void AdminPresetList()
    {
        _cyclePreset.ListAllPresets(Context);
    }

    [Command("presetstartvote", "presetvotestart"), RequireAdmin]
    public void AdminPresetVoteStart()
    {
        _cyclePreset.StartVote(Context);
    }

    [Command("presetfinishvote", "presetvotefinish"), RequireAdmin]
    public void AdminPresetVoteFinish()
    {
        _cyclePreset.FinishVote(Context);
    }

    [Command("presetcancelvote", "presetvotecancel"), RequireAdmin]
    public void AdminPresetVoteCancel()
    {
        _cyclePreset.CancelVote(Context);
    }

    [Command("presetextendvote", "presetvoteextend"), RequireAdmin]
    public void AdminPresetVoteExtend(int seconds)
    {
        _cyclePreset.ExtendVote(Context, seconds);
    }

    [Command("presetset", "presetchange", "presetuse", "presetupdate"), RequireAdmin]
    public void AdminPresetSet(int choice)
    {
        _cyclePreset.SetPreset(Context, choice);
    }

    [Command("presetrandom"), RequireAdmin]
    public void AdminPresetRandom()
    {
        _cyclePreset.RandomPreset(Context);
    }
}
