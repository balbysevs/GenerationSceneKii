using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SceneGenerator.Desktop.Services;

public sealed class StorageOutputFolderPicker : IOutputFolderPicker
{
    private readonly TopLevel _owner;

    public StorageOutputFolderPicker(TopLevel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<string?> PickFolderAsync()
    {
        if (!_owner.StorageProvider.CanPickFolder)
        {
            throw new InvalidOperationException("Выбор папки недоступен на этом устройстве.");
        }

        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку для JSON-файлов сцен",
            AllowMultiple = false
        });
        if (folders.Count == 0)
        {
            return null;
        }

        using var folder = folders[0];
        return folder.TryGetLocalPath()
            ?? throw new InvalidOperationException("Выберите папку на этом устройстве.");
    }
}
