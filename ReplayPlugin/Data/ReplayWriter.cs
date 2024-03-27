using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ReplayPlugin.Data;

public class ReplayWriter : BinaryWriter
{
    public ReplayWriter(Stream output) : base(output)
    {
        
    }
    
    public void WriteStruct<T>(T value, [CallerArgumentExpression("value")] string? message = null) where T : unmanaged
    {
        var before = OutStream.Position;
        Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)));
        var after = OutStream.Position;
        //Log.Debug("Size of {0}: {1}", message, after - before);
    }

    public void WritePadding(int count)
    {
        Write(stackalloc byte[count]);
    }

    public void WriteString(string? str)
    {
        str ??= "";
        Write((uint)str.Length);
        Write(Encoding.UTF8.GetBytes(str));
    }
}
