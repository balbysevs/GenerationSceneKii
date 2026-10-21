using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record Point3DDto(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z);
