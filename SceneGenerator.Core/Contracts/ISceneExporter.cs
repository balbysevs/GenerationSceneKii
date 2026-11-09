using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Contracts;

public interface ISceneExporter
{
    /// <summary>Сохраняет одну сцену в файл по указанному пути.</summary>
    void Export(Scene scene, string filePath);
}
