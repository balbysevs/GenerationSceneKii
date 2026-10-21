using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record RadarPlacementZoneDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("objectId")]
    public required string ObjectId { get; init; }

    [JsonPropertyName("surface")]
    public string Surface { get; init; } = "roof";

    [JsonPropertyName("allowedPositions")]
    public required IReadOnlyList<string> AllowedPositions { get; init; }

}
