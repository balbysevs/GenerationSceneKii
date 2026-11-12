using System.Text.Json;
using RadarPlacement.Core.Geometry;
using RadarPlacement.Core.Models;
using RadarPlacement.Serialization.Internal;

namespace RadarPlacement.Serialization;

/// <summary>
/// Читает формат экспорта генератора schemaVersion 1.0: один КИИ, режим 2D,
/// прямоугольники вдоль осей, без внутренних контуров. Не использует код первой команды.
/// Неизвестные дополнительные поля сохраняет, известные ограничения проверяет строго.
/// </summary>
public sealed class SceneJsonReader2D
{
    /// <summary>Разбирает строку JSON и возвращает документ вместе с моделью ядра.</summary>
    public SceneDocument2D Parse(string json, JsonReadLimits? limits = null)
    {
        var root = JsonInput.Parse(json, limits);
        JsonInput.Kind(root, JsonValueKind.Object, "$");
        if (root.TryGetProperty("radars", out _) || root.TryGetProperty("solver", out _))
            throw new RadarJsonException("$", "Это документ результата. Загрузите исходную сцену генератора.");
        JsonInput.Expect(root, "schemaVersion", "1.0", "$");
        JsonInput.Expect(root, "mode", "2D", "$");
        string sceneId = JsonInput.String(root, "sceneId", "$");
        ValidateConventions(root);
        var boundsJson = JsonInput.Object(root, "bounds", "$");
        var min = ReadPoint3DAs2D(JsonInput.Object(boundsJson, "min", "$.bounds"), "$.bounds.min");
        var max = ReadPoint3DAs2D(JsonInput.Object(boundsJson, "max", "$.bounds"), "$.bounds.max");
        var bounds = CreateRectangle(min.X, min.Y, max.X, max.Y, "$.bounds");

        var objects = JsonInput.Array(root, "objects", "$");
        if (objects.GetArrayLength() == 0) throw new RadarJsonException("$.objects", "Сцена должна содержать КИИ.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var obstacles = new List<Rectangle2D>();
        Rectangle2D? kii = null;
        string kiiId = "";
        int index = 0;
        foreach (var item in objects.EnumerateArray())
        {
            string path = $"$.objects[{index}]";
            string id = JsonInput.String(item, "id", path);
            if (!ids.Add(id)) throw new RadarJsonException(path + ".id", "Идентификаторы объектов должны быть уникальны.");
            string type = JsonInput.String(item, "type", path);
            if (type != (index == 0 ? "kii" : "obstacle"))
                throw new RadarJsonException(path + ".type", "Первым должен быть единственный КИИ, далее — только obstacle.");
            var rectangle = ReadGeometry(JsonInput.Object(item, "geometry", path), path + ".geometry");
            if (!bounds.Contains(rectangle)) throw new RadarJsonException(path + ".geometry", "Объект выходит за границы сцены.");
            if (index == 0) { kii = rectangle; kiiId = id; }
            else
            {
                if (rectangle.OverlapsInterior(kii!) || obstacles.Any(rectangle.OverlapsInterior))
                    throw new RadarJsonException(path + ".geometry", "Внутренность объекта пересекается с ранее записанным объектом.");
                obstacles.Add(rectangle);
            }
            index++;
        }
        ValidatePlacement(root, kiiId);
        return new(new Scene2D(bounds, kii!, obstacles), sceneId, kiiId, root);
    }

    /// <summary>Синхронно читает UTF-8-файл. Для оконного приложения предпочтительнее LoadAsync.</summary>
    public SceneDocument2D Load(string path, JsonReadLimits? limits = null) =>
        LoadAsync(path, limits).GetAwaiter().GetResult();

    /// <summary>Асинхронно читает файл с ограничением размера и поддержкой отмены.</summary>
    public async Task<SceneDocument2D> LoadAsync(string path, JsonReadLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        string json = await JsonFiles.ReadAsync(path, limits, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var result = Parse(json, limits);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static void ValidateConventions(JsonElement root)
    {
        var coordinates = JsonInput.Object(root, "coordinateSystem", "$");
        JsonInput.Expect(coordinates, "type", "localCartesian", "$.coordinateSystem");
        JsonInput.Expect(coordinates, "units", "meters", "$.coordinateSystem");
        JsonInput.Expect(coordinates, "xAxis", "east", "$.coordinateSystem");
        JsonInput.Expect(coordinates, "yAxis", "north", "$.coordinateSystem");
        JsonInput.Expect(coordinates, "zAxis", "up", "$.coordinateSystem");
        var terrain = JsonInput.Object(root, "terrain", "$");
        JsonInput.Expect(terrain, "type", "flat", "$.terrain");
        JsonInput.Zero(terrain, "elevation", "$.terrain");
        var observation = JsonInput.Object(root, "observationArea", "$");
        JsonInput.Expect(observation, "type", "terrainSurface", "$.observationArea");
        JsonInput.Expect(observation, "extent", "sceneBounds", "$.observationArea");
        JsonInput.Kind(JsonInput.Required(observation, "excludeObjectFootprints", "$.observationArea"),
            JsonValueKind.True, "$.observationArea.excludeObjectFootprints");
        var source = JsonInput.Object(root, "source", "$");
        string sourceType = JsonInput.String(source, "type", "$.source");
        if (sourceType is not ("generated" or "manual" or "openstreetmap"))
            throw new RadarJsonException("$.source.type", "Неизвестный источник сцены.");
    }

    private static void ValidatePlacement(JsonElement root, string kiiId)
    {
        var zones = JsonInput.Array(root, "radarPlacementZones", "$");
        const string path = "$.radarPlacementZones[0]";
        if (zones.GetArrayLength() != 1)
            throw new RadarJsonException("$.radarPlacementZones", "Для одного КИИ требуется ровно одна зона размещения.");
        var zone = zones[0];
        JsonInput.String(zone, "id", path);
        JsonInput.Expect(zone, "objectId", kiiId, path);
        JsonInput.Expect(zone, "surface", "roof", path);
        var positions = JsonInput.Array(zone, "allowedPositions", path);
        // Ядро всегда рассматривает и периметр, и центр. Нельзя молча проигнорировать
        // более строгий список разрешений и выдать недопустимую установку.
        var values = positions.EnumerateArray().Select(p =>
            JsonInput.Kind(p, JsonValueKind.String, path + ".allowedPositions").GetString()).ToArray();
        if (values.Length != 2 || !values.Contains("outerBoundary") || !values.Contains("center"))
            throw new RadarJsonException(path + ".allowedPositions", "Требуются ровно outerBoundary и center в любом порядке.");
    }

    private static Rectangle2D ReadGeometry(JsonElement geometry, string path)
    {
        JsonInput.Expect(geometry, "type", "polygonPrism", path);
        JsonInput.Zero(geometry, "baseElevation", path);
        JsonInput.Zero(geometry, "height", path);
        var footprint = JsonInput.Object(geometry, "footprint", path);
        string footprintPath = path + ".footprint";
        if (JsonInput.Array(footprint, "holes", footprintPath).GetArrayLength() != 0)
            throw new RadarJsonException(footprintPath + ".holes", "Внутренние контуры пока не поддерживаются.");
        var outer = JsonInput.Array(footprint, "outer", footprintPath);
        if (outer.GetArrayLength() != 4)
            throw new RadarJsonException(footprintPath + ".outer", "Требуются четыре вершины без повторения первой в конце.");
        var points = outer.EnumerateArray().Select((p, i) => new Point2D(
            JsonInput.Number(p, "x", $"{footprintPath}.outer[{i}]"),
            JsonInput.Number(p, "y", $"{footprintPath}.outer[{i}]"))).ToArray();
        var rectangle = CreateRectangle(points.Min(p => p.X), points.Min(p => p.Y),
            points.Max(p => p.X), points.Max(p => p.Y), footprintPath + ".outer");
        // Одного bounding box недостаточно: иначе повёрнутый прямоугольник или
        // самопересекающийся контур незаметно превратится в совсем другое препятствие.
        if (points.Distinct().Count() != 4 || points.Any(p =>
                (p.X != rectangle.MinX && p.X != rectangle.MaxX) ||
                (p.Y != rectangle.MinY && p.Y != rectangle.MaxY)))
            throw new RadarJsonException(footprintPath + ".outer", "Поддерживаются только прямоугольники вдоль осей.");
        for (int i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            if (!((a.X == b.X) ^ (a.Y == b.Y)))
                throw new RadarJsonException(footprintPath + ".outer", "Вершины должны идти по периметру, без диагоналей.");
        }
        return rectangle;
    }

    private static Point2D ReadPoint3DAs2D(JsonElement point, string path)
    {
        JsonInput.Zero(point, "z", path);
        return new(JsonInput.Number(point, "x", path), JsonInput.Number(point, "y", path));
    }

    private static Rectangle2D CreateRectangle(double minX, double minY, double maxX, double maxY, string path)
    {
        try { return new(minX, minY, maxX, maxY); }
        catch (ArgumentException exception) { throw new RadarJsonException(path, exception.Message, exception); }
    }
}
