using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Generation;
using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeButtonText))]
    public partial bool IsDarkTheme { get; set; }

    public string ThemeButtonText => IsDarkTheme ? "Светлая тема" : "Тёмная тема";

    [ObservableProperty]
    public partial bool IsThreeD { get; set; } = true;

    [ObservableProperty]
    public partial decimal? SceneLength { get; set; } = 10000m;

    [ObservableProperty]
    public partial decimal? SceneWidth { get; set; } = 10000m;

    [ObservableProperty]
    public partial decimal? SceneHeight { get; set; } = 40m;

    [ObservableProperty]
    public partial decimal? KiiLength { get; set; } = 200m;

    [ObservableProperty]
    public partial decimal? KiiWidth { get; set; } = 100m;

    [ObservableProperty]
    public partial decimal? KiiHeight { get; set; } = 20m;

    [ObservableProperty]
    public partial decimal? ObstacleCount { get; set; } = 20m;

    [ObservableProperty]
    public partial decimal? ObstacleMinLength { get; set; } = 20m;

    [ObservableProperty]
    public partial decimal? ObstacleMaxLength { get; set; } = 100m;

    [ObservableProperty]
    public partial decimal? ObstacleMinWidth { get; set; } = 20m;

    [ObservableProperty]
    public partial decimal? ObstacleMaxWidth { get; set; } = 100m;

    [ObservableProperty]
    public partial decimal? ObstacleMinHeight { get; set; } = 3m;

    [ObservableProperty]
    public partial decimal? ObstacleMaxHeight { get; set; } = 40m;

    [ObservableProperty]
    public partial decimal? DistanceToNearestObstacle { get; set; } = 500m;

    [ObservableProperty]
    public partial decimal? MinDistanceBetweenObstacles { get; set; } = 30m;

    [ObservableProperty]
    public partial decimal? SceneCount { get; set; } = 10m;

    [ObservableProperty]
    public partial decimal? Seed { get; set; } = 12345m;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings))]
    public partial bool IsGenerating { get; set; }

    public bool CanEditSettings => !IsGenerating;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Готово к генерации";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public bool HasErrors => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    public partial IReadOnlyList<Scene> Scenes { get; set; } = Array.Empty<Scene>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSceneSummary))]
    public partial Scene? SelectedScene { get; set; }

    public string SelectedSceneSummary => SelectedScene is { } scene
        ? $"{scene.Id}\n" +
          $"Режим: {(scene.Mode == SceneMode.ThreeD ? "3D" : "2D")}\n" +
          $"Объектов: {scene.Objects.Count}\n" +
          $"Размеры: {scene.Bounds.Size.Length:0.##} × {scene.Bounds.Size.Width:0.##} × " +
          $"{scene.Bounds.Size.Height:0.##} м"
        : "Сгенерируйте сцены и выберите одну в списке справа.";

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

    [RelayCommand]
    private async Task GenerateAsync()
    {
        ErrorMessage = string.Empty;
        GenerationSettings settings;
        try
        {
            settings = CreateSettings();
            IReadOnlyList<string> errors = new GenerationSettingsValidator().Validate(settings);
            if (errors.Count > 0)
            {
                ErrorMessage = string.Join(Environment.NewLine, errors);
                StatusText = "Проверьте параметры генерации.";
                return;
            }
        }
        catch (ArgumentException error)
        {
            ErrorMessage = error.Message;
            StatusText = "Проверьте параметры генерации.";
            return;
        }

        IsGenerating = true;
        StatusText = "Генерация сцен…";
        try
        {
            GeneratedSceneSource generatedSource = new(settings);
            ISceneSource sceneSource = generatedSource;
            Scene[] generatedScenes = await Task.Run(() => sceneSource.GetScenes().ToArray());

            Scenes = Array.AsReadOnly(generatedScenes);
            SelectedScene = Scenes.FirstOrDefault();
            StatusText = $"Сгенерировано сцен: {Scenes.Count}. Seed: {generatedSource.Seed}.";
        }
        catch (InvalidOperationException error)
        {
            ErrorMessage = error.Message;
            StatusText = "Не удалось сгенерировать сцены. Предыдущий результат сохранён.";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private GenerationSettings CreateSettings()
    {
        return new GenerationSettings
        {
            Mode = IsThreeD ? SceneMode.ThreeD : SceneMode.TwoD,
            SceneSize = new Size3D(
                (double)Required(SceneLength, "Длина сцены"),
                (double)Required(SceneWidth, "Ширина сцены"),
                IsThreeD ? (double)Required(SceneHeight, "Высота сцены") : 0),
            KiiSize = new Size3D(
                (double)Required(KiiLength, "Длина КИИ"),
                (double)Required(KiiWidth, "Ширина КИИ"),
                IsThreeD ? (double)Required(KiiHeight, "Высота КИИ") : 0),
            ObstacleSizes = new ObstacleSizeRanges(
                ReadRange(ObstacleMinLength, ObstacleMaxLength, "Длина помех"),
                ReadRange(ObstacleMinWidth, ObstacleMaxWidth, "Ширина помех"),
                IsThreeD
                    ? ReadRange(ObstacleMinHeight, ObstacleMaxHeight, "Высота помех")
                    : new DoubleRange(0, 0)),
            ObstacleCount = ReadInteger(ObstacleCount, "Количество помех"),
            DistanceToNearestObstacle = (double)Required(DistanceToNearestObstacle, "Расстояние до ближайшей помехи"),
            MinDistanceBetweenObstacles = (double)Required(MinDistanceBetweenObstacles, "Минимальное расстояние между помехами"),
            SceneCount = ReadInteger(SceneCount, "Количество сцен"),
            Seed = Seed is null ? null : ReadInteger(Seed, "Seed")
        };
    }

    private static decimal Required(decimal? value, string label)
    {
        return value ?? throw new ArgumentException($"Заполните поле «{label}».");
    }

    private static int ReadInteger(decimal? value, string label)
    {
        decimal number = Required(value, label);
        if (number != decimal.Truncate(number) || number < int.MinValue || number > int.MaxValue)
        {
            throw new ArgumentException($"Поле «{label}» должно содержать целое число в пределах типа int.");
        }

        return (int)number;
    }

    private static DoubleRange ReadRange(decimal? min, decimal? max, string label)
    {
        decimal minimum = Required(min, $"{label}: минимум");
        decimal maximum = Required(max, $"{label}: максимум");
        if (minimum > maximum)
        {
            throw new ArgumentException($"{label}: минимум не должен превышать максимум.");
        }

        return new DoubleRange((double)minimum, (double)maximum);
    }
}
