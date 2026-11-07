using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SceneGenerator.Desktop.Services;

public sealed class StorageSettingsFilePicker : ISettingsFilePicker
{
    private readonly TopLevel _owner;
    private static readonly FilePickerFileType JsonFiles = new("Файлы JSON")
    {
        Patterns = ["*.json"]
    };

    public StorageSettingsFilePicker(TopLevel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<string?> PickOpenFileAsync()
    {
        if (!_owner.StorageProvider.CanOpen)
        {
            throw new InvalidOperationException("Выбор файла недоступен на этом устройстве.");
        }

        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Загрузить настройки генерации",
            AllowMultiple = false,
            FileTypeFilter = [JsonFiles]
        });
        if (files.Count == 0) return null;

        using var file = files[0];
        return file.TryGetLocalPath()
            ?? throw new InvalidOperationException("Выберите файл на этом устройстве.");
    }

    public async Task<string?> PickSaveFileAsync()
    {
        if (!_owner.StorageProvider.CanSave)
        {
            throw new InvalidOperationException("Сохранение файла недоступно на этом устройстве.");
        }

        using var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить настройки генерации",
            SuggestedFileName = "settings.json",
            DefaultExtension = "json",
            FileTypeChoices = [JsonFiles],

            ShowOverwritePrompt = false
        });
        return file is null ? null : file.TryGetLocalPath()
            ?? throw new InvalidOperationException("Выберите файл на этом устройстве.");
    }
}
