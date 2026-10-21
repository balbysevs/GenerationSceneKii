using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record FootprintDto
{
    [JsonPropertyName("outer")]
    public required IReadOnlyList<Point2DDto> Outer { get; init; }

    [JsonPropertyName("holes")]
    public required IReadOnlyList<IReadOnlyList<Point2DDto>> Holes { get; init; }

}
