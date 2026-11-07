using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Models;
using SceneGenerator.OpenStreetMap;

namespace SceneGenerator.Desktop.ViewModels;

public partial class OsmImportViewModel : ViewModelBase
{
    private readonly IOsmDataProvider _onlineProvider;
    private readonly Func<IOsmDataProvider>? _onlineProviderFactory;
    private readonly Func<Task<string?>> _pickFile;
    private readonly Func<Task<string?>>? _pickSaveFile;
    private CancellationTokenSource? _cancellation;
    private OsmImportDocument? _document;
    public event Action<ISceneSource>? SceneReady;

    [ObservableProperty] public partial string OverpassEndpoint { get; set; } = OverpassDataProvider.DefaultEndpoint.ToString();
    [ObservableProperty] public partial decimal? Latitude { get; set; } = 57.6261m;
    [ObservableProperty] public partial decimal? Longitude { get; set; } = 39.8845m;
    [ObservableProperty] public partial decimal? AreaLength { get; set; } = 1000m;
    [ObservableProperty] public partial decimal? AreaWidth { get; set; } = 1000m;
    [ObservableProperty] public partial decimal? FloorHeight { get; set; } = 3m;
    [ObservableProperty] public partial decimal? DefaultHeight { get; set; } = 12m;
    [ObservableProperty] public partial bool IsThreeD { get; set; } = true;
    [ObservableProperty] public partial double LoadedLength { get; set; } = 1000;
    [ObservableProperty] public partial double LoadedWidth { get; set; } = 1000;
    [ObservableProperty] public partial string StatusText { get; set; } = "Укажите центр участка и его размеры. Загрузите здания или откройте JSON-ответ Overpass.";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    public partial string ErrorMessage { get; set; } = "";
    public bool HasErrors => !string.IsNullOrEmpty(ErrorMessage);
    [ObservableProperty] public partial string QualityText { get; set; } = "Рельеф ровный. Крыши плоские. Контуры и высоты OSM могут требовать уточнения.";
    [ObservableProperty] public partial IReadOnlyList<OsmBuildingItemViewModel> Buildings { get; set; } = Array.Empty<OsmBuildingItemViewModel>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateSceneCommand))]
    [NotifyPropertyChangedFor(nameof(CreateSceneHint))]
    public partial OsmBuildingItemViewModel? SelectedBuilding { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyCanExecuteChangedFor(nameof(LoadOnlineCommand), nameof(OpenFileCommand), nameof(CreateSceneCommand), nameof(SaveProjectCommand))]
    public partial bool IsBusy { get; set; }

    public int KiiCount => Buildings.Count(b => b.IsKii);

    partial void OnBuildingsChanged(IReadOnlyList<OsmBuildingItemViewModel> oldValue, IReadOnlyList<OsmBuildingItemViewModel> newValue)
    {
        foreach (var item in oldValue) item.PropertyChanged -= BuildingChanged;
        foreach (var item in newValue) item.PropertyChanged += BuildingChanged;
        UpdateKiiSelection();
    }
    private void BuildingChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(OsmBuildingItemViewModel.IsKii) or nameof(OsmBuildingItemViewModel.Included)) UpdateKiiSelection();
    }
    private void UpdateKiiSelection()
    {
        OnPropertyChanged(nameof(KiiCount));
        OnPropertyChanged(nameof(CreateSceneHint));
        CreateSceneCommand.NotifyCanExecuteChanged();
    }

    public string CreateSceneHint => _document is null
        ? "Сначала загрузите здания или откройте проект импорта."
        : KiiCount == 0
            ? "Отметьте хотя бы одно здание галочкой «КИИ»."
            : $"Выбрано КИИ: {KiiCount}. Нажмите «Использовать сцену» для перехода к просмотру.";

    public bool CanEdit => !IsBusy;
    private bool CanSaveProject => !IsBusy && _document is not null && _pickSaveFile is not null;
    private bool CanCreate => !IsBusy && _document is not null && KiiCount > 0;

    public OsmImportViewModel(IOsmDataProvider onlineProvider, Func<Task<string?>> pickFile, Func<Task<string?>>? pickSaveFile = null, Func<IOsmDataProvider>? onlineProviderFactory = null)
    {
        _onlineProvider = onlineProvider;
        _onlineProviderFactory = onlineProviderFactory;
        _pickFile = pickFile;
        _pickSaveFile = pickSaveFile;
    }

    [RelayCommand] public void Cancel() => _cancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task LoadOnlineAsync() => LoadAsync(_onlineProvider, useOnlineFactory: true);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task OpenFileAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            string? path = await _pickFile();
            if (path is not null) await LoadAsync(new FileOsmDataProvider(path), alreadyBusy: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { ErrorMessage = error.Message; }
        finally { IsBusy = false; }
    }

    private OsmImportOptions ReadOptions() => new()
    {
        Latitude = Read(Latitude, "Широта"), Longitude = Read(Longitude, "Долгота"),
        Length = Read(AreaLength, "Длина участка"), Width = Read(AreaWidth, "Ширина участка"),
        FloorHeight = Read(FloorHeight, "Высота этажа"), DefaultHeight = Read(DefaultHeight, "Высота по умолчанию"),
        Mode = IsThreeD ? SceneMode.ThreeD : SceneMode.TwoD
    };

    private async Task LoadAsync(IOsmDataProvider provider, bool alreadyBusy = false, bool useOnlineFactory = false)
    {
        if (IsBusy && !alreadyBusy) return;
        IsBusy = true;
        ErrorMessage = "";
        _cancellation = new CancellationTokenSource();
        try
        {
            var options = ReadOptions();
            options.Validate();
            StatusText = "Получение данных и проверка контуров…";
            if (useOnlineFactory && _onlineProviderFactory is not null) provider = _onlineProviderFactory();
            var document = await new OsmImportService(provider).LoadAsync(options, _cancellation.Token);
            if (document.Buildings.Count == 0)
                throw new InvalidOperationException("Внутри выбранной области не найдено подходящих зданий. " +
                    string.Join(" ", document.Issues.Take(3)) + " Измените центр или увеличьте участок.");
            _cancellation.Token.ThrowIfCancellationRequested();
            _document = document;
            options = document.Options;
            Buildings = document.Buildings.Select(b => new OsmBuildingItemViewModel(b)).ToArray();
            var savedChoices = document.SavedProject?.Selections.ToDictionary(s => s.Id);
            foreach (var item in Buildings)
                if (savedChoices is not null && savedChoices.TryGetValue(item.Id, out var choice))
                {
                    item.Included = choice.Included;
                    item.TopHeight = (decimal)choice.TopHeight;
                }
            var savedKiiIds = document.SavedProject?.EffectiveKiiIds;
            foreach (var item in Buildings) item.IsKii = savedKiiIds?.Contains(item.Id) == true;
            SelectedBuilding = Buildings.FirstOrDefault(b => b.IsKii);
            Latitude = (decimal)options.Latitude; Longitude = (decimal)options.Longitude;
            AreaLength = (decimal)options.Length; AreaWidth = (decimal)options.Width;
            FloorHeight = (decimal)options.FloorHeight; DefaultHeight = (decimal)options.DefaultHeight;
            IsThreeD = options.Mode == SceneMode.ThreeD;
            LoadedLength = options.Length;
            LoadedWidth = options.Width;
            int known = document.Buildings.Count(b => b.Height.Origin == HeightOrigin.OsmHeight);
            int levels = document.Buildings.Count(b => b.Height.Origin == HeightOrigin.OsmLevels);
            QualityText = $"Зданий: {Buildings.Count}. Высота из OSM: {known}; по этажности: {levels}; " +
                $"по умолчанию: {Buildings.Count - known - levels}.\n" +
                $"Предупреждений: {document.Issues.Count}. Рельеф ровный, крыши плоские; building:part не моделируются.\n" +
                string.Join("\n", document.Issues.Take(12)) + (document.Issues.Count > 12 ? "\n…" : "");
            StatusText = "Отметьте хотя бы одно здание галочкой «КИИ». Изменения центра и размеров применяются при следующей загрузке.";
            OnPropertyChanged(nameof(CreateSceneHint));
            CreateSceneCommand.NotifyCanExecuteChanged();
            SaveProjectCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException) { StatusText = "Загрузка отменена. Прежний результат сохранён."; }
        catch (Exception error) when (error is ArgumentException or FormatException or JsonException or IOException or
            UnauthorizedAccessException or HttpRequestException or InvalidOperationException)
        {
            ErrorMessage = error.Message;
            StatusText = "Импорт не выполнен. Прежний результат сохранён.";
        }
        finally
        {
            _cancellation.Dispose(); _cancellation = null;
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateSceneAsync()
    {
        if (!CanCreate) return;
        ErrorMessage = "";
        IsBusy = true;
        _cancellation = new CancellationTokenSource();
        try
        {
            var selections = Buildings.Select(b => new OsmBuildingSelection(b.Id,
                IsThreeD && b.Included ? Read(b.TopHeight, $"Высота {b.Id}") : b.Building.Height.Top, b.Included, b.IsHeightEdited)).ToArray();
            var reviewed = _document! with { Options = _document.Options with { Mode = IsThreeD ? SceneMode.ThreeD : SceneMode.TwoD } };
            string[] kiiIds = Buildings.Where(b => b.IsKii).Select(b => b.Id).ToArray();
            StatusText = "Создание сцены и проверка пересечений…";
            OsmSceneSource source = await Task.Run(() => new OsmSceneSource(reviewed, kiiIds, selections, _cancellation.Token), _cancellation.Token);
            _cancellation.Token.ThrowIfCancellationRequested();
            foreach (var skipped in source.SkippedBuildings)
            {
                var item = Buildings.FirstOrDefault(b => b.Id == skipped.Id);
                if (item is not null) item.Included = false;
            }
            if (source.SkippedBuildings.Count > 0)
                QualityText += $"\nАвтоматически исключено зданий: {source.SkippedBuildings.Count}.\n" +
                    string.Join("\n", source.SkippedBuildings.Select(b => $"{b.Id}: {b.Reason}"));
            SceneReady?.Invoke(source);
        }
        catch (OperationCanceledException) { StatusText = "Создание сцены отменено."; }
        catch (ArgumentException error) { ErrorMessage = error.Message; StatusText = "Проверьте выбор зданий и высоты."; }
        finally
        {
            _cancellation.Dispose(); _cancellation = null;
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveProject))]
    private async Task SaveProjectAsync()
    {
        if (!CanSaveProject) return;
        IsBusy = true; ErrorMessage = "";
        try
        {
            string? path = await _pickSaveFile!();
            if (path is null) return;
            var project = new OsmImportProject
            {
                Options = _document!.Options with { Mode = IsThreeD ? SceneMode.ThreeD : SceneMode.TwoD },
                RawOverpassJson = _document.RawJson, Source = _document.Source, LoadedAt = _document.LoadedAt,
                Format = KiiCount > 1 ? "scene-generator-osm-project-v2" : "scene-generator-osm-project-v1",
                KiiId = KiiCount == 1 ? Buildings.Single(b => b.IsKii).Id : null,
                KiiIds = KiiCount > 1 ? Buildings.Where(b => b.IsKii).Select(b => b.Id).ToArray() : null,
                Issues = _document.Issues,
                Selections = Buildings.Select(b => new OsmBuildingSelection(b.Id,
                    Read(b.TopHeight, $"Высота {b.Id}"), b.Included, b.IsHeightEdited)).ToArray()
            };
            await project.SaveAsync(path);
            StatusText = "Проект импорта сохранён: исходные данные, привязка и правки высот. Это отдельный файл, не JSON сцены для решателя.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or FormatException or JsonException)
        { ErrorMessage = error.Message; }
        finally { IsBusy = false; }
    }

    private static double Read(decimal? value, string field) => value is null
        ? throw new ArgumentException($"Заполните поле «{field}».") : (double)value.Value;
}
