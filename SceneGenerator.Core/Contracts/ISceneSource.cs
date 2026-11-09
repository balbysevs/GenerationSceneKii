using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Contracts;

public interface ISceneSource
{
    /// <summary>Возвращает последовательность сцен, предоставляемых генератором или импортом.</summary>
    IEnumerable<Scene> GetScenes();
}
