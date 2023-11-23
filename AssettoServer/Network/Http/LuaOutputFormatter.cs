using System.Text;
using System.Threading.Tasks;
using Luaon.Json;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace AssettoServer.Network.Http;

public class LuaOutputFormatter : TextOutputFormatter
{
    public LuaOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/x-lua"));
        SupportedEncodings.Add(Encoding.UTF8);
    }
    
    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var serializer = new JsonSerializer();
        await using var sw = context.WriterFactory(context.HttpContext.Response.Body, selectedEncoding);
        using var jlw = new JsonLuaWriter(sw);
        jlw.CloseOutput = false;
        jlw.Formatting = Formatting.None;

        serializer.Serialize(jlw, context.Object);
        await sw.FlushAsync();
    }
}
