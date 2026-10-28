using System.Threading.Tasks;

namespace SceneGenerator.Desktop.Services;

public interface IOutputFolderPicker
{
    Task<string?> PickFolderAsync();
}
