using SceneGenerator.Core.Configuration;

namespace SceneGenerator.Core.Contracts;

public interface IGenerationSettingsWriter
{
    void Write(GenerationSettings settings, string filePath, bool overwrite = false);
}
