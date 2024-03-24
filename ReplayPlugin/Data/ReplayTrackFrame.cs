using System.Numerics;

namespace ReplayPlugin.Data;

public struct ReplayTrackFrame
{
    public Half SunAngle; 
    public Half SomethingElse; // That's literally what i got from the binary template
    //public TrackObject[] TrackObjects;

    public void ToWriter(ReplayWriter writer)
    {
        writer.Write(SunAngle);
        writer.Write(SomethingElse);

        /*foreach (var trackObject in TrackObjects)
        {
            writer.WriteStruct(trackObject.Pos);
            writer.WriteStruct(trackObject.Rotation);
        }*/
    }
}

public struct TrackObject
{
    public Vector3 Pos; 
    public Vector3 Rotation;
}
