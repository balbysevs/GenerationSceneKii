using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record SceneDto
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("sceneId")]
    public required string SceneId { get; init; }

    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("coordinateSystem")]
    public CoordinateSystemDto CoordinateSystem { get; init; } = new();

    [JsonPropertyName("source")]
    public required SceneSourceDto Source { get; init; }

    [JsonPropertyName("bounds")]
    public required BoundsDto Bounds { get; init; }

    [JsonPropertyName("terrain")]
    public required TerrainDto Terrain { get; init; }

    [JsonPropertyName("objects")]
    public required IReadOnlyList<SceneObjectDto> Objects { get; init; }

    [JsonPropertyName("radarPlacementZones")]
    public required IReadOnlyList<RadarPlacementZoneDto> RadarPlacementZones { get; init; }

    [JsonPropertyName("observationArea")]
    public ObservationAreaDto ObservationArea { get; init; } = new();

}
