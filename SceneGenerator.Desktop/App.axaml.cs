using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SceneGenerator.Desktop.Services;
using SceneGenerator.Desktop.ViewModels;
using SceneGenerator.Desktop.Views;

namespace SceneGenerator.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            window.DataContext = new MainViewModel(
                new StorageOutputFolderPicker(window),
                new WindowConfirmationService(window),
                new StorageSettingsFilePicker(window),
                new WindowOsmImportDialog(window));
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
