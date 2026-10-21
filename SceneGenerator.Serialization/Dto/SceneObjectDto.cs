using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record SceneObjectDto
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("geometry")]
    public required GeometryDto Geometry { get; init; }

}
