
using System.Collections.Generic;

namespace AssettoServer.Server.Configuration.Kunos;

public class LapInfo
{
    public string? DriverName { get; set; }
    public ulong  DriverGuid { get; set; } = 0;
    public byte CarId { get; set; }
    public string CarModel { get; set; } = "";
    public string CarSkin { get; set; } = "";
    public uint LapNumber { get; set; }
    public long Timestamp { get; set; }
    public uint LapTime { get; set; } 
    public List<uint> Sectors { get; set; } = new List<uint> { 0, 0, 0 };
    public byte Cuts { get; set; } = 0;
    public int BallastKG { get; set; } = 0;
    public string Tyre { get; set; } = "H";
    public int Restrictor { get; set; } = 0;
}

