using System.Text.Json;
using System.Text.Json.Serialization;
using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Serialization;

public sealed class JsonGenerationSettingsReader : IGenerationSettingsReader
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters = { new SceneModeConverter() }
    };

    public GenerationSettings Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string json = File.ReadAllText(filePath);
        GenerationSettings settings;

        try
        {
            settings = JsonSerializer.Deserialize<GenerationSettings>(json, _options)
                ?? throw new FormatException("Файл настроек должен содержать объект JSON, а не null.");
        }
        catch (JsonException error)
        {
            throw new FormatException(
                $"Не удалось разобрать JSON настроек. Проверьте структуру и типы значений. " +
                $"Поле: {error.Path ?? "$"}. {error.Message}", error);
        }
        catch (ArgumentException error)
        {
            throw new FormatException($"Некорректные значения настроек: {error.Message}", error);
        }

        IReadOnlyList<string> errors = new GenerationSettingsValidator().Validate(settings);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "Ошибки параметров:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        return settings;
    }

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
