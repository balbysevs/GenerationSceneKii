using SceneGenerator.Core.Geometry;

namespace SceneGenerator.OpenStreetMap;

public interface ICoordinateProjector
{
    Point2D Project(double latitude, double longitude);
}



public sealed class LocalCoordinateProjector : ICoordinateProjector
{
    private readonly double _lat, _lon, _x, _y, _z, _offsetX, _offsetY;

    public LocalCoordinateProjector(OsmImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _lat = options.Latitude * Math.PI / 180;
        _lon = options.Longitude * Math.PI / 180;
        (_x, _y, _z) = ToEcef(_lat, _lon);
        _offsetX = options.Length / 2;
        _offsetY = options.Width / 2;
    }

    public Point2D Project(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || Math.Abs(latitude) > 90 ||
            !double.IsFinite(longitude) || Math.Abs(longitude) > 180)
            throw new ArgumentException("Некорректные географические координаты здания.");
        double latitudeRadians = latitude * Math.PI / 180;
        double longitudeRadians = longitude * Math.PI / 180;
        double alignment = Math.Sin(latitudeRadians) * Math.Sin(_lat) +
            Math.Cos(latitudeRadians) * Math.Cos(_lat) * Math.Cos(longitudeRadians - _lon);
        if (alignment < 0.99)
            throw new ArgumentException("Координаты здания слишком далеко от выбранной локальной области.");
        var (x, y, z) = ToEcef(latitudeRadians, longitudeRadians);
        double dx = x - _x, dy = y - _y, dz = z - _z;
        return new Point2D(_offsetX - Math.Sin(_lon) * dx + Math.Cos(_lon) * dy,
            _offsetY - Math.Sin(_lat) * Math.Cos(_lon) * dx - Math.Sin(_lat) * Math.Sin(_lon) * dy + Math.Cos(_lat) * dz);
    }

    private static (double X, double Y, double Z) ToEcef(double latitude, double longitude)
    {
        const double a = 6378137;
        const double e2 = 6.6943799901413165e-3;
        double sin = Math.Sin(latitude), cos = Math.Cos(latitude);
        double n = a / Math.Sqrt(1 - e2 * sin * sin);
        return (n * cos * Math.Cos(longitude), n * cos * Math.Sin(longitude), n * (1 - e2) * sin);
    }
}
