using System.Text.Json;
using System.Text.Json.Serialization;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Serialization;

internal static class GenerationSettingsJson
{
    internal static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        WriteIndented = true,
        Converters = { new SceneModeConverter() }
    };

    private sealed class SceneModeConverter : JsonConverter<SceneMode>
    {
        public override SceneMode Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Режим должен быть строкой: 2D или 3D.");
            }

            return reader.GetString() switch
            {
                "2D" => SceneMode.TwoD,
                "3D" => SceneMode.ThreeD,
                _ => throw new JsonException("Допустимые режимы: 2D и 3D.")
            };
        }

        public override void Write(Utf8JsonWriter writer, SceneMode value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value switch
            {
                SceneMode.TwoD => "2D",
                SceneMode.ThreeD => "3D",
                _ => throw new JsonException("Неизвестный режим сцены.")
            });
        }
    }
}
