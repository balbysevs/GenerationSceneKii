using System.Text;
using System.Text.Json;
using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Contracts;

namespace SceneGenerator.Serialization;

public sealed class JsonGenerationSettingsWriter : IGenerationSettingsWriter
{
    /// <summary>Проверяет настройки и сохраняет их в JSON; существующий файл заменяется только с разрешения вызывающего кода.</summary>
    public void Write(GenerationSettings settings, string filePath, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        IReadOnlyList<string> errors = new GenerationSettingsValidator().Validate(settings);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "Ошибки параметров:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        string json = JsonSerializer.Serialize(settings, GenerationSettingsJson.Options) + Environment.NewLine;
        string fullPath = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {

            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, fullPath, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
