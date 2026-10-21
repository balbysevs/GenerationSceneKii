using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record Point2DDto(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);
