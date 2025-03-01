using System.Text.Json.Serialization;

namespace CalyLogViewer
{
    [JsonSerializable(typeof(CalyLogItem), GenerationMode = JsonSourceGenerationMode.Metadata)]
    internal partial class SourceGenerationContext : JsonSerializerContext
    { }
}
