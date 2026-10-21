using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SceneGenerator.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeButtonText))]
    public partial bool IsDarkTheme { get; set; }

    public string ThemeButtonText => IsDarkTheme ? "Светлая тема" : "Тёмная тема";

    public MainViewModel()
    {
        IsDarkTheme = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        IsDarkTheme = !IsDarkTheme;
        app.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
