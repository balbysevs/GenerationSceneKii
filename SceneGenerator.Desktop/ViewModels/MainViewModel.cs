using System;
using System.Collections.Generic;
using System.IO;
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
using SceneGenerator.Desktop.Services;
using SceneGenerator.Serialization;
using SceneGenerator.Serialization.Dto;

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

    private readonly IOutputFolderPicker? _outputFolderPicker;
    private readonly IConfirmationService? _confirmationService;
    private readonly ISettingsFilePicker? _settingsFilePicker;
    private readonly IGenerationSettingsReader _settingsReader = new JsonGenerationSettingsReader();
    private readonly IGenerationSettingsWriter _settingsWriter = new JsonGenerationSettingsWriter();
    private Dictionary<Scene, SceneSourceDto> _sceneSources = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand), nameof(SaveSelectedCommand), nameof(SaveAllCommand),
        nameof(DeleteSelectedCommand), nameof(ClearScenesCommand),
        nameof(LoadSettingsCommand), nameof(SaveSettingsCommand), nameof(ImportOsmCommand))]
    public partial bool IsGenerating { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand), nameof(SaveSelectedCommand), nameof(SaveAllCommand),
        nameof(DeleteSelectedCommand), nameof(ClearScenesCommand),
        nameof(LoadSettingsCommand), nameof(SaveSettingsCommand), nameof(ImportOsmCommand))]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand), nameof(SaveSelectedCommand), nameof(SaveAllCommand),
        nameof(DeleteSelectedCommand), nameof(ClearScenesCommand),
        nameof(LoadSettingsCommand), nameof(SaveSettingsCommand), nameof(ImportOsmCommand))]
    public partial bool IsConfirming { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSettings))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand), nameof(SaveSelectedCommand), nameof(SaveAllCommand),
        nameof(DeleteSelectedCommand), nameof(ClearScenesCommand),
        nameof(LoadSettingsCommand), nameof(SaveSettingsCommand), nameof(ImportOsmCommand))]
    public partial bool IsWorkingWithSettings { get; set; }

    public bool CanEditSettings => !IsGenerating && !IsSaving && !IsConfirming && !IsWorkingWithSettings;
    private bool CanUseSettingsFiles => _settingsFilePicker is not null && CanEditSettings;
    private bool CanGenerate => CanEditSettings;
    private bool CanSaveAll => _outputFolderPicker is not null && CanEditSettings && Scenes.Count > 0;
    private bool CanSaveSelected => CanSaveAll && SelectedScene is not null && Scenes.Contains(SelectedScene);
    private bool CanClearScenes => _confirmationService is not null && CanEditSettings && Scenes.Count > 0;
    private bool CanDeleteSelected => CanClearScenes && SelectedScene is not null && Scenes.Contains(SelectedScene);

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Готово к генерации";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public bool HasErrors => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSelectedCommand), nameof(SaveAllCommand),
        nameof(DeleteSelectedCommand), nameof(ClearScenesCommand))]
    public partial IReadOnlyList<Scene> Scenes { get; set; } = Array.Empty<Scene>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSceneSummary))]
    [NotifyCanExecuteChangedFor(nameof(SaveSelectedCommand), nameof(DeleteSelectedCommand))]
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

    public MainViewModel(IOutputFolderPicker outputFolderPicker) : this()
    {
        ArgumentNullException.ThrowIfNull(outputFolderPicker);
        _outputFolderPicker = outputFolderPicker;
    }

    public MainViewModel(IOutputFolderPicker outputFolderPicker, IConfirmationService confirmationService)
        : this(outputFolderPicker)
    {
        ArgumentNullException.ThrowIfNull(confirmationService);
        _confirmationService = confirmationService;
    }

    public MainViewModel(IOutputFolderPicker outputFolderPicker, IConfirmationService confirmationService,
        ISettingsFilePicker settingsFilePicker) : this(outputFolderPicker, confirmationService)
    {
        ArgumentNullException.ThrowIfNull(settingsFilePicker);
        _settingsFilePicker = settingsFilePicker;
    }

    [RelayCommand(CanExecute = nameof(CanUseSettingsFiles))]
    private async Task LoadSettingsAsync()
    {
        if (!CanUseSettingsFiles) return;
        IsWorkingWithSettings = true;
        ErrorMessage = string.Empty;
        try
        {
            string? path = await _settingsFilePicker!.PickOpenFileAsync();
            if (path is null)
            {
                StatusText = "Загрузка настроек отменена.";
                return;
            }

            StatusText = "Загрузка настроек…";
            GenerationSettings settings = await Task.Run(() => _settingsReader.Read(path));
            ApplySettings(settings);
            StatusText = $"Настройки загружены: {Path.GetFullPath(path)}. Для создания сцен нажмите «Сгенерировать».";
        }
        catch (OverflowException)
        {
            ErrorMessage = "Одно из значений слишком велико для поля ввода.";
            StatusText = "Не удалось загрузить настройки. Прежние параметры сохранены.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or FormatException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            ErrorMessage = error.Message;
            StatusText = "Не удалось загрузить настройки. Прежние параметры сохранены.";
        }
        finally
        {
            IsWorkingWithSettings = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseSettingsFiles))]
    private async Task SaveSettingsAsync()
    {
        if (!CanUseSettingsFiles) return;
        ErrorMessage = string.Empty;
        GenerationSettings settings;
        try
        {
            settings = CreateSettings();
            IReadOnlyList<string> errors = new GenerationSettingsValidator().Validate(settings);
            if (errors.Count > 0)
            {
                ErrorMessage = string.Join(Environment.NewLine, errors);
                StatusText = "Проверьте параметры перед сохранением настроек.";
                return;
            }
        }
        catch (ArgumentException error)
        {
            ErrorMessage = error.Message;
            StatusText = "Проверьте параметры перед сохранением настроек.";
            return;
        }

        IsWorkingWithSettings = true;
        try
        {
            string? path = await _settingsFilePicker!.PickSaveFileAsync();
            if (path is null)
            {
                StatusText = "Сохранение настроек отменено.";
                return;
            }

            bool overwrite = File.Exists(path);
            if (overwrite && (_confirmationService is null ||
                !await _confirmationService.ConfirmAsync("Заменить файл настроек?",
                    $"Файл «{Path.GetFileName(path)}» уже существует. Заменить его текущими настройками?")))
            {
                StatusText = "Сохранение настроек отменено. Существующий файл сохранён.";
                return;
            }

            StatusText = "Сохранение настроек…";
            await Task.Run(() => _settingsWriter.Write(settings, path, overwrite));
            StatusText = $"Настройки сохранены: {Path.GetFullPath(path)}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            ErrorMessage = error.Message;
            StatusText = "Не удалось сохранить настройки.";
        }
        finally
        {
            IsWorkingWithSettings = false;
        }
    }

    private void ApplySettings(GenerationSettings settings)
    {
        // Convert the only unbounded double before changing any form fields.
        decimal nearestDistance = (decimal)settings.DistanceToNearestObstacle;
        SceneLength = (decimal)settings.SceneSize.Length;
        SceneWidth = (decimal)settings.SceneSize.Width;
        KiiLength = (decimal)settings.KiiSize.Length;
        KiiWidth = (decimal)settings.KiiSize.Width;
        ObstacleMinLength = (decimal)settings.ObstacleSizes.Length.Min;
        ObstacleMaxLength = (decimal)settings.ObstacleSizes.Length.Max;
        ObstacleMinWidth = (decimal)settings.ObstacleSizes.Width.Min;
        ObstacleMaxWidth = (decimal)settings.ObstacleSizes.Width.Max;
        ObstacleCount = settings.ObstacleCount;
        DistanceToNearestObstacle = nearestDistance;
        MinDistanceBetweenObstacles = (decimal)settings.MinDistanceBetweenObstacles;
        SceneCount = settings.SceneCount;
        Seed = settings.Seed;
        IsThreeD = settings.Mode == SceneMode.ThreeD;
        if (IsThreeD)
        {
            SceneHeight = (decimal)settings.SceneSize.Height;
            KiiHeight = (decimal)settings.KiiSize.Height;
            ObstacleMinHeight = (decimal)settings.ObstacleSizes.Height.Min;
            ObstacleMaxHeight = (decimal)settings.ObstacleSizes.Height.Max;
        }
        // Hidden 3D fields keep their values when loading 2D; CreateSettings writes zero heights for 2D.
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

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (!CanGenerate) return;
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
            Scene[] generatedScenes = await Task.Run(() =>
            {
                // Reserve the batch size and avoid temporary iterator-to-array buffers.
                var scenes = new List<Scene>(settings.SceneCount);
                foreach (Scene scene in sceneSource.GetScenes()) scenes.Add(scene);
                return scenes.ToArray();
            });

            _sceneSources = generatedScenes.Select((scene, index) => (scene, source: new SceneSourceDto
            {
                Type = "generated",
                Seed = generatedSource.Seed,
                SequenceIndex = index + 1
            })).ToDictionary(item => item.scene, item => item.source);
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

    [RelayCommand(CanExecute = nameof(CanSaveSelected))]
    private Task SaveSelectedAsync() => SaveScenesAsync(saveAll: false);

    [RelayCommand(CanExecute = nameof(CanSaveAll))]
    private Task SaveAllAsync() => SaveScenesAsync(saveAll: true);

    private async Task SaveScenesAsync(bool saveAll)
    {
        if (_outputFolderPicker is null || !(saveAll ? CanSaveAll : CanSaveSelected)) return;

        // Capture the scenes and their generation metadata before opening the dialog.
        Scene[] allScenes = Scenes.ToArray();
        Scene[] scenes = saveAll ? allScenes : new[] { SelectedScene! };
        var exports = scenes.Select(scene =>
        {
            SceneSourceDto source = _sceneSources.TryGetValue(scene, out SceneSourceDto? savedSource)
                ? savedSource : new SceneSourceDto { Type = "manual" };
            int index = source.SequenceIndex ?? Array.IndexOf(allScenes, scene) + 1;
            return (Scene: scene, Source: source, FileName: $"scene-{index:D6}.json");
        }).ToArray();

        IsSaving = true;
        ErrorMessage = string.Empty;
        int savedCount = 0;
        string? outputFolder = null;
        try
        {
            outputFolder = await _outputFolderPicker.PickFolderAsync();
            if (outputFolder is null)
            {
                StatusText = "Сохранение отменено.";
                return;
            }

            outputFolder = Path.GetFullPath(outputFolder);
            StatusText = "Сохранение сцен…";
            await Task.Run(() =>
            {
                // Check the whole batch before writing; the exporter also refuses overwrites.
                foreach (var item in exports)
                {
                    string path = Path.Combine(outputFolder, item.FileName);
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        throw new IOException($"«{item.FileName}» уже существует. Выберите другую папку.");
                    }
                }

                foreach (var item in exports)
                {
                    ISceneExporter exporter = new JsonSceneExporter(item.Source);
                    exporter.Export(item.Scene, Path.Combine(outputFolder, item.FileName));
                    savedCount++;
                }
            });
            StatusText = $"Сохранено сцен: {savedCount}. Папка: {outputFolder}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            ErrorMessage = error.Message;
            StatusText = $"Сохранено сцен: {savedCount} из {exports.Length}." +
                (outputFolder is null ? string.Empty : $" Папка: {outputFolder}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private Task DeleteSelectedAsync() => RemoveScenesAsync(removeAll: false);

    [RelayCommand(CanExecute = nameof(CanClearScenes))]
    private Task ClearScenesAsync() => RemoveScenesAsync(removeAll: true);

    private async Task RemoveScenesAsync(bool removeAll)
    {
        if (_confirmationService is null || !(removeAll ? CanClearScenes : CanDeleteSelected)) return;
        IReadOnlyList<Scene> originalList = Scenes;
        Scene? selected = SelectedScene;
        int selectedIndex = selected is null ? -1 : Array.IndexOf(originalList.ToArray(), selected);
        string title = removeAll ? "Очистить список сцен?" : "Удалить сцену?";
        string message = (removeAll
            ? $"Удалить все сцены из текущего списка ({originalList.Count})?"
            : $"Удалить сцену «{selected!.Id}» из текущего списка?")
            + Environment.NewLine + "Сохранённые JSON-файлы останутся в своих папках.";

        IsConfirming = true;
        ErrorMessage = string.Empty;
        try
        {
            if (!await _confirmationService.ConfirmAsync(title, message))
            {
                StatusText = "Удаление отменено.";
                return;
            }
            if (!ReferenceEquals(originalList, Scenes))
            {
                StatusText = "Список сцен изменился. Повторите удаление.";
                return;
            }

            if (removeAll)
            {
                SelectedScene = null;
                Scenes = Array.Empty<Scene>();
                _sceneSources.Clear();
                StatusText = $"Список очищен. Удалено сцен: {originalList.Count}.";
            }
            else
            {
                Scene[] remaining = originalList.Where(scene => !ReferenceEquals(scene, selected)).ToArray();
                _sceneSources.Remove(selected!);
                Scenes = Array.AsReadOnly(remaining);
                SelectedScene = remaining.Length == 0 ? null : remaining[Math.Min(selectedIndex, remaining.Length - 1)];
                StatusText = $"Сцена «{selected!.Id}» удалена из списка. Осталось: {Scenes.Count}.";
            }
        }
        catch (InvalidOperationException error)
        {
            ErrorMessage = error.Message;
            StatusText = "Не удалось открыть подтверждение удаления.";
        }
        finally
        {
            IsConfirming = false;
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
