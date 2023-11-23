using System.IO;
using JetBrains.Annotations;
using Luaon.Json;
using Newtonsoft.Json;

namespace AssettoServer.Utils;

public static class Luaon
{
    [PublicAPI]
    public static string Serialize(object obj)
    {
        var serializer = new JsonSerializer();
        using var sw = new StringWriter();
        using (var jlw = new JsonLuaWriter(sw))
        {
            jlw.CloseOutput = false;
            jlw.Formatting = Formatting.None;
            serializer.Serialize(jlw, obj);
        }
        return sw.ToString();
    }
}
