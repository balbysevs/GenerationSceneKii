using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record TerrainDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "flat";

    [JsonPropertyName("elevation")]
    public required double Elevation { get; init; }

}
