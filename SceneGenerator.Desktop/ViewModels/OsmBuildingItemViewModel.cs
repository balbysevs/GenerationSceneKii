using CommunityToolkit.Mvvm.ComponentModel;
using SceneGenerator.OpenStreetMap;

namespace SceneGenerator.Desktop.ViewModels;

public partial class OsmBuildingItemViewModel : ViewModelBase
{
    public OsmBuilding Building { get; }
    public string Id => Building.Id;
    public string Label => $"{Building.Name} · {Id}";
    public string GroundLabel => $"Отметка нижней поверхности: {Building.Height.Base:0.##} м";

    [ObservableProperty]
    public partial bool Included { get; set; } = true;

    [ObservableProperty]
    public partial bool IsKii { get; set; }

    partial void OnIsKiiChanged(bool value) { if (value) Included = true; }
    partial void OnIncludedChanged(bool value) { if (!value) IsKii = false; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeightDescription), nameof(IsEstimated))]
    public partial decimal? TopHeight { get; set; }

    public bool IsHeightEdited => TopHeight is not null && (double)TopHeight.Value != Building.Height.Top;
    public bool IsEstimated => Building.Height.Origin != HeightOrigin.OsmHeight && !IsHeightEdited;
    public string HeightDescription => IsHeightEdited ? "Введено вручную" : Building.Height.Origin switch
    {
        HeightOrigin.OsmHeight => "Высота указана в OSM (не проверена обмерами)",
        HeightOrigin.OsmLevels => "Оценка по этажности и параметрам крыши",
        _ => "Высота по умолчанию — требуется уточнение"
    };

    public OsmBuildingItemViewModel(OsmBuilding building)
    {
        Building = building;
        TopHeight = (decimal)building.Height.Top;
    }
}
