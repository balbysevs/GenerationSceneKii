using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Contracts;

public interface ISceneSource
{
    IEnumerable<Scene> GetScenes();
}
