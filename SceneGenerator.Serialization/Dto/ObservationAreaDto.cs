using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record ObservationAreaDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "terrainSurface";

    [JsonPropertyName("extent")]
    public string Extent { get; init; } = "sceneBounds";

    [JsonPropertyName("excludeObjectFootprints")]
    public bool ExcludeObjectFootprints { get; init; } = true;

}
