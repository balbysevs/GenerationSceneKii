using SceneGenerator.Core.Configuration;

namespace SceneGenerator.Core.Contracts;

public interface IGenerationSettingsReader
{
    /// <summary>Загружает параметры генерации из файла по указанному пути.</summary>
    GenerationSettings Read(string filePath);
}
