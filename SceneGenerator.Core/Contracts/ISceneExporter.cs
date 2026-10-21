using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Contracts;

public interface ISceneExporter
{
    void Export(Scene scene, string filePath);
}
