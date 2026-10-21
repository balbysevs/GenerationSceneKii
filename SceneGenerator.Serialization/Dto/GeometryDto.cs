using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record GeometryDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "polygonPrism";

    [JsonPropertyName("footprint")]
    public required FootprintDto Footprint { get; init; }

    [JsonPropertyName("baseElevation")]
    public required double BaseElevation { get; init; }

    [JsonPropertyName("height")]
    public required double Height { get; init; }

}
