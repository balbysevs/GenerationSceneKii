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
    [ObservableProperty]
    public partial bool IsThreeD { get; set; } = true;

    [ObservableProperty]
    public partial decimal SceneLength { get; set; } = 10000m;

    [ObservableProperty]
    public partial decimal SceneWidth { get; set; } = 10000m;

    [ObservableProperty]
    public partial decimal SceneHeight { get; set; } = 40m;
    [ObservableProperty]
    public partial decimal KiiLength { get; set; } = 200m;

    [ObservableProperty]
    public partial decimal KiiWidth { get; set; } = 100m;

    [ObservableProperty]
    public partial decimal KiiHeight { get; set; } = 20m;
    [ObservableProperty]
    public partial decimal ObstacleCount { get; set; } = 20m;

    [ObservableProperty]
    public partial decimal ObstacleMinLength { get; set; } = 20m;

    [ObservableProperty]
    public partial decimal ObstacleMaxLength { get; set; } = 100m;

    [ObservableProperty]
    public partial decimal ObstacleMinWidth { get; set; } = 20m;

    [ObservableProperty]
    public partial decimal ObstacleMaxWidth { get; set; } = 100m;

    [ObservableProperty]
    public partial decimal ObstacleMinHeight { get; set; } = 3m;

    [ObservableProperty]
    public partial decimal ObstacleMaxHeight { get; set; } = 40m;
}
