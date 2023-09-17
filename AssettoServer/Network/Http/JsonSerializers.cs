using System.Text.Json.Serialization;
using AssettoServer.Shared.Network.Http.Responses;

namespace AssettoServer.Network.Http;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(InfoResponse))]
internal partial class InfoResponseSourceGenerationContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(EntryListResponse))]
internal partial class EntryListResponseSourceGenerationContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DetailResponse))]
internal partial class DetailResponseSourceGenerationContext : JsonSerializerContext
{
}
