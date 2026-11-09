using SceneGenerator.Core.Configuration;

namespace SceneGenerator.Core.Contracts;

public interface IGenerationSettingsWriter
{
    /// <summary>Сохраняет параметры генерации в файл; заменяет существующий файл только при разрешённой перезаписи.</summary>
    void Write(GenerationSettings settings, string filePath, bool overwrite = false);
}
