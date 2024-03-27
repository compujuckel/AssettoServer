using ReplayPlugin.Utils;

namespace ReplayPlugin.Data;

public class ReplayCar
{
    public string CarId;
    public string DriverName;
    public string NationCode;
    public string DriverTeam;
    public string CarSkinId;
    
    public uint NumberOfWings;

    public List<ReplayCarFrame> Frames = new();

    public LapInfo[] Laps;

    public void ToWriter(BinaryWriter writer, uint numberArg)
    {
        writer.WriteACString(CarId);
        writer.WriteACString(DriverName);
        writer.WriteACString(NationCode);
        writer.WriteACString(DriverTeam);
        writer.WriteACString(CarSkinId);
        
        writer.WriteStruct((uint)Frames.Count);
        writer.WriteStruct(NumberOfWings);
        foreach (var frame in Frames)
        {
            var before = writer.BaseStream.Position;
            frame.ToWriter(writer, true); // , NumberOfWings);
            var after = writer.BaseStream.Position;
            //Log.Debug("Full frame size: {0}", after - before);
        }

        writer.WriteStruct((uint)Laps.Length);
        foreach (var lap in Laps)
        {
            writer.WriteStruct(lap.Time);
            writer.WriteStruct(lap.IsValid);
        }
    }
}

public struct LapInfo
{
    public uint Time; 
    public uint IsValid;
}
