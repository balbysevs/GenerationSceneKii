using SceneGenerator.Core.Configuration;

namespace SceneGenerator.Core.Contracts;

public interface IGenerationSettingsReader
{
    GenerationSettings Read(string filePath);
}
