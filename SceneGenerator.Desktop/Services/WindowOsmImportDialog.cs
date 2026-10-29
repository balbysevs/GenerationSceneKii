using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Desktop.ViewModels;
using SceneGenerator.Desktop.Views;
using SceneGenerator.OpenStreetMap;

namespace SceneGenerator.Desktop.Services;

public sealed class WindowOsmImportDialog(Window owner) : IOsmImportDialog
{
    private static readonly HttpClient Client = new() { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    public async Task<ISceneSource?> ShowAsync()
    {
        var window = new OsmImportWindow();
        var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SceneGeneratorKii", "OsmCache");
        OsmImportViewModel? model = null;
        model = new OsmImportViewModel(new OverpassDataProvider(Client, cache), async () =>
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Открыть проект импорта или JSON-ответ Overpass (out body geom)", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON Overpass") { Patterns = ["*.json"] }]
            });
            if (files.Count == 0) return null;
            using var file = files[0];
            return file.TryGetLocalPath() ?? throw new InvalidOperationException("Выберите локальный файл.");
        }, async () =>
        {
            using var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить проект импорта OSM", SuggestedFileName = "territory.osm-project.json",
                DefaultExtension = "json", ShowOverwritePrompt = false
            });
            return file?.TryGetLocalPath();
        }, () => new OverpassDataProvider(Client, cache,
            Uri.TryCreate(model!.OverpassEndpoint, UriKind.Absolute, out var endpoint)
                ? endpoint : throw new ArgumentException("Укажите полный HTTPS-адрес сервера Overpass.")));
        window.DataContext = model;
        model.SceneReady += source => window.Close(source);
        return await window.ShowDialog<ISceneSource?>(owner);
    }
}
