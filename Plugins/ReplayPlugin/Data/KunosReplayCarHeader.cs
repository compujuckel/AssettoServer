using ReplayPlugin.Utils;

namespace ReplayPlugin.Data;

public struct KunosReplayCarHeader
{
    public string? CarId;
    public string? DriverName;
    public string? NationCode;
    public string? DriverTeam;
    public string? CarSkinId;
    public uint CarFrames;
    public uint NumberOfWings;

    public void ToWriter(BinaryWriter writer)
    {
        writer.WriteACString(CarId);
        writer.WriteACString(DriverName);
        writer.WriteACString(NationCode);
        writer.WriteACString(DriverTeam);
        writer.WriteACString(CarSkinId);
        writer.Write(CarFrames);
        writer.Write(NumberOfWings);
    }
}
