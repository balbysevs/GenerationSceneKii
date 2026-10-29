using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Desktop.Services;
using SceneGenerator.OpenStreetMap;
using SceneGenerator.Serialization.Dto;

namespace SceneGenerator.Desktop.ViewModels;

public partial class MainViewModel
{
    private readonly SceneGenerator.Desktop.Services.IOsmImportDialog? _osmImportDialog;
    public MainViewModel(IOutputFolderPicker folderPicker, IConfirmationService confirmation,
        ISettingsFilePicker settingsPicker, IOsmImportDialog osmDialog) : this(folderPicker, confirmation, settingsPicker)
    {
        System.ArgumentNullException.ThrowIfNull(osmDialog);
        _osmImportDialog = osmDialog;
    }

    private bool CanImportOsm => _osmImportDialog is not null && CanEditSettings;

    [RelayCommand(CanExecute = nameof(CanImportOsm))]
    private async Task ImportOsmAsync()
    {
        if (!CanImportOsm) return;
        IsWorkingWithSettings = true;
        try
        {
            ISceneSource? source = await _osmImportDialog!.ShowAsync();
            if (source is null) return;
            var imported = source.GetScenes().ToArray();
            if (imported.Length == 0) return;
            _sceneSources = imported.ToDictionary(scene => scene, _ => new SceneSourceDto { Type = "openstreetmap" });
            Scenes = imported;
            SelectedScene = imported[0];
            ErrorMessage = string.Empty;
            StatusText = $"Импорт OSM: объектов {imported[0].Objects.Count}. Рельеф ровный; возможны оценочные высоты.";
            if (source is OsmSceneSource osm && osm.SkippedBuildings.Count > 0)
            {
                StatusText += $" Автоматически исключено конфликтующих зданий: {osm.SkippedBuildings.Count}. " +
                    string.Join(", ", osm.SkippedBuildings.Take(3).Select(b => b.Id)) +
                    (osm.SkippedBuildings.Count > 3 ? "…" : ".");
            }
        }
        catch (System.Exception error) when (error is System.InvalidOperationException or System.ArgumentException or System.IO.IOException)
        {
            ErrorMessage = error.Message;
            StatusText = "Не удалось открыть импорт OSM. Прежние сцены сохранены.";
        }
        finally { IsWorkingWithSettings = false; }
    }
}
