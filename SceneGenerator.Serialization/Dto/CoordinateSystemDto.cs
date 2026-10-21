using System.Text.Json.Serialization;

namespace SceneGenerator.Serialization.Dto;

public sealed record CoordinateSystemDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "localCartesian";

    [JsonPropertyName("units")]
    public string Units { get; init; } = "meters";

    [JsonPropertyName("xAxis")]
    public string XAxis { get; init; } = "east";

    [JsonPropertyName("yAxis")]
    public string YAxis { get; init; } = "north";

    [JsonPropertyName("zAxis")]
    public string ZAxis { get; init; } = "up";

}
