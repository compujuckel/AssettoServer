using System.Text.Json.Serialization;
using AssettoServer.Shared.Network.Http.Responses;

namespace AssettoServer.Network.Http;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(InfoResponse))]
[JsonSerializable(typeof(DetailResponse))]
[JsonSerializable(typeof(EntryListResponse))]
internal partial class JsonSourceGenerationContext : JsonSerializerContext
{
}
