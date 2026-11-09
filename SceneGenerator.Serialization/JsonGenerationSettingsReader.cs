using System.Text.Json;
using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Contracts;

namespace SceneGenerator.Serialization;

public sealed class JsonGenerationSettingsReader : IGenerationSettingsReader
{
    /// <summary>Читает настройки из JSON и проверяет их перед запуском генерации.</summary>
    public GenerationSettings Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string json = File.ReadAllText(filePath);
        GenerationSettings settings;

        try
        {
            settings = JsonSerializer.Deserialize<GenerationSettings>(json, GenerationSettingsJson.Options)
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
}
