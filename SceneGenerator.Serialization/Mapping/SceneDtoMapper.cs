using SceneGenerator.Core.Geometry;
using SceneGenerator.Core.Models;
using SceneGenerator.Serialization.Dto;

namespace SceneGenerator.Serialization.Mapping;

public sealed class SceneDtoMapper
{
    /// <summary>Преобразует сцену в модель обмена с решателем, включая версию формата и правила размещения радаров для каждого КИИ.</summary>
    public SceneDto ToDto(Scene scene, SceneSourceDto source)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(source);
        if (source.Type is not ("manual" or "generated" or "openstreetmap"))
            throw new ArgumentException("Неизвестный источник сцены.", nameof(source));
        if (source.SequenceIndex is < 1)
            throw new ArgumentOutOfRangeException(nameof(source), "Номер сцены должен быть положительным.");
        if (source.Type != "generated" && (source.Seed is not null || source.SequenceIndex is not null))
            throw new ArgumentException("Seed и номер в серии относятся только к генерации.", nameof(source));

        var kiis = scene.KiiObjects;

        return new SceneDto
        {
            SchemaVersion = kiis.Count > 1 ? "1.1" : "1.0",
            SceneId = scene.Id,
            Mode = scene.Mode switch
            {
                SceneMode.TwoD => "2D",
                SceneMode.ThreeD => "3D",
                _ => throw new ArgumentOutOfRangeException(nameof(scene))
            },
            Source = source with { },
            Bounds = new BoundsDto
            {
                Min = ToDto(scene.Bounds.Origin),
                Max = new Point3DDto(
                    scene.Bounds.Footprint.MaxX,
                    scene.Bounds.Footprint.MaxY,
                    scene.Bounds.BaseElevation + scene.Bounds.Height)
            },
            Terrain = new TerrainDto { Elevation = scene.Bounds.BaseElevation },
            Objects = Array.AsReadOnly(scene.Objects.Select(ToDto).ToArray()),
            RadarPlacementZones = Array.AsReadOnly(kiis.Select((kii, index) => new RadarPlacementZoneDto
                {
                    Id = kiis.Count == 1 ? "kii-placement-zone" : $"kii-placement-zone-{index + 1}",
                    ObjectId = kii.Id,
                    AllowedPositions = Array.AsReadOnly(new[] { "outerBoundary", "center" })
                }).ToArray())
        };
    }

    /// <summary>Переносит тип и геометрию объекта: внешнее основание, отверстия, отметку основания и высоту.</summary>
    private static SceneObjectDto ToDto(SceneObject item) => new()
    {
        Id = item.Id,
        Type = item.Type switch
        {
            SceneObjectType.Kii => "kii",
            SceneObjectType.Obstacle => "obstacle",
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        },
        Geometry = new GeometryDto
        {
            BaseElevation = item.Geometry.BaseElevation,
            Height = item.Geometry.Height,
            Footprint = new FootprintDto
            {
                Outer = Array.AsReadOnly(item.Geometry.Footprint.Outer.Select(ToDto).ToArray()),
                Holes = Array.AsReadOnly(item.Geometry.Footprint.Holes
                    .Select(h => (IReadOnlyList<Point2DDto>)Array.AsReadOnly(h.Select(ToDto).ToArray()))
                    .ToArray())
            }
        }
    };

    /// <summary>Копирует координаты X и Y вершины основания в модель экспорта.</summary>
    private static Point2DDto ToDto(Point2D point) => new(point.X, point.Y);
    /// <summary>Копирует координаты X, Y и Z пространственной точки в модель экспорта.</summary>
    private static Point3DDto ToDto(Point3D point) => new(point.X, point.Y, point.Z);
}
