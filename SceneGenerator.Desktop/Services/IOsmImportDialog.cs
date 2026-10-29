using System.Threading.Tasks;
using SceneGenerator.Core.Contracts;

namespace SceneGenerator.Desktop.Services;

public interface IOsmImportDialog
{
    Task<ISceneSource?> ShowAsync();
}
