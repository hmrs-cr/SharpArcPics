using System.Text.Json.Serialization;

namespace PicArchiver.Core.Configs;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ArchiveConfig))]
internal partial class ArchiveConfigSourceGenerationContext : JsonSerializerContext
{
}