using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record SceneSourceDto
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("seed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seed { get; init; } = null;

    [JsonPropertyName("sequenceIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SequenceIndex { get; init; } = null;

}
