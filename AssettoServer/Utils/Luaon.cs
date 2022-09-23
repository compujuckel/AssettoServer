using System.IO;
using Luaon.Json;
using Newtonsoft.Json;

namespace AssettoServer.Utils;

public static class Luaon
{
    public static string Serialize(object obj)
    {
        var serializer = new JsonSerializer();
        using var sw = new StringWriter();
        using (var jlw = new JsonLuaWriter(sw) { CloseOutput = false, Formatting = Formatting.None })
        {
            serializer.Serialize(jlw, obj);
        }
        return sw.ToString();
    }
}
