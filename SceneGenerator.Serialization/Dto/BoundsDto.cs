using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record BoundsDto
{
    [JsonPropertyName("min")]
    public required Point3DDto Min { get; init; }

    [JsonPropertyName("max")]
    public required Point3DDto Max { get; init; }

}
