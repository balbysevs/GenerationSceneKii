using SceneGenerator.Core.Models;

namespace SceneGenerator.OpenStreetMap;

public sealed record OsmImportOptions
{
    public double Latitude { get; init; } = 57.6261;
    public double Longitude { get; init; } = 39.8845;
    public double Length { get; init; } = 1000;
    public double Width { get; init; } = 1000;
    public SceneMode Mode { get; init; } = SceneMode.ThreeD;
    public double FloorHeight { get; init; } = 3;
    public double DefaultHeight { get; init; } = 12;

    public void Validate()
    {
        if (!double.IsFinite(Latitude) || Math.Abs(Latitude) > 80 ||
            !double.IsFinite(Longitude) || Math.Abs(Longitude) > 180)
            throw new ArgumentException("Координаты центра: широта от −80 до 80°, долгота от −180 до 180°.");
        if (!double.IsFinite(Length) || Length < 100 || Length > 20000 ||
            !double.IsFinite(Width) || Width < 100 || Width > 20000)
            throw new ArgumentException("Размеры импортируемого участка должны быть от 100 до 20000 м.");
        if (!Enum.IsDefined(Mode)) throw new ArgumentException("Неизвестный режим сцены.");
        if (!double.IsFinite(FloorHeight) || FloorHeight < 1 || FloorHeight > 10 ||
            !double.IsFinite(DefaultHeight) || DefaultHeight <= 0 || DefaultHeight > 1000)
            throw new ArgumentException("Высота этажа: 1–10 м; высота по умолчанию: больше 0 и не более 1000 м.");
        _ = GetQueryBounds();
    }

    public GeographicBounds GetQueryBounds()
    {
        // A conservative geographic envelope around the local rectangle, with a margin.
        double radius = double.Hypot(Length, Width) / 2 + 100;
        double latitudeDelta = radius / 6330000 * 180 / Math.PI;
        double longitudeDelta = latitudeDelta / Math.Cos((Math.Abs(Latitude) + latitudeDelta) * Math.PI / 180);
        if (Longitude - longitudeDelta < -180 || Longitude + longitudeDelta > 180)
            throw new ArgumentException("Участки, пересекающие меридиан 180°, пока не поддерживаются.");
        return new(Latitude - latitudeDelta, Longitude - longitudeDelta,
            Latitude + latitudeDelta, Longitude + longitudeDelta);
    }
}

public sealed record GeographicBounds(double South, double West, double North, double East);
