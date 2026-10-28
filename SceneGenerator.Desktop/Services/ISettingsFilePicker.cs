using System.Threading.Tasks;

namespace SceneGenerator.Desktop.Services;

public interface ISettingsFilePicker
{
    Task<string?> PickOpenFileAsync();
    Task<string?> PickSaveFileAsync();
}
