using System.Text.Json.Serialization;

namespace ApixPress.App.Models.DTOs;

public sealed class AppUpdateManifestItemDto
{
    [JsonPropertyName("PacketName")]
    public string PacketName { get; init; } = string.Empty;

    [JsonPropertyName("Hash")]
    public string Hash { get; init; } = string.Empty;

    [JsonPropertyName("Version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("Url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("PubTime")]
    public DateTime PubTime { get; init; }
}
