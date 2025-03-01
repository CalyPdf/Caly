using System.Text.Json.Serialization;
using Caly.Core.Loggers;
using Caly.Core.Models;

namespace Caly.Core.Utilities
{
    [JsonSerializable(typeof(CalySettings), GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(CalyLogItem), GenerationMode = JsonSourceGenerationMode.Metadata)]
    internal partial class SourceGenerationContext : JsonSerializerContext
    { }
}
