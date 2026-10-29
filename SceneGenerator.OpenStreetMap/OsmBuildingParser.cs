using System.Text.Json;
using SceneGenerator.Core.Geometry;

namespace SceneGenerator.OpenStreetMap;

public sealed class OsmBuildingParser
{
    public OsmImportDocument Parse(OsmData data, OsmImportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (data.Json.Length > 32 * 1024 * 1024) throw new FormatException("Файл OSM превышает лимит 32 МиБ символов.");
        using var document = JsonDocument.Parse(data.Json, new JsonDocumentOptions { MaxDepth = 64 });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("Ожидается JSON-ответ Overpass API.");
        if (root.TryGetProperty("remark", out _))
            throw new FormatException("Overpass вернул диагностическое сообщение. Частичный ответ не импортирован.");
        if (!root.TryGetProperty("elements", out var array) || array.ValueKind != JsonValueKind.Array)
            throw new FormatException("В файле отсутствует массив elements. Нужен ответ Overpass в формате JSON с out geom.");
        if (array.GetArrayLength() > 50000) throw new FormatException("Слишком много элементов: уменьшите область импорта.");

        var projector = new LocalCoordinateProjector(options);
        var buildings = new List<OsmBuilding>();
        var issues = new List<string>();
        var consumedWays = new HashSet<long>();
        var identifiers = new HashSet<string>();
        var elements = array.EnumerateArray().ToArray();
        foreach (var element in elements.OrderBy(e => Text(e, "type") == "relation" ? 0 : 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string type = Text(element, "type");
            if (type is not ("way" or "relation")) continue;
            var tags = Tags(element);
            if (!tags.TryGetValue("building", out var buildingTag) || buildingTag == "no") continue;
            if (!element.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out long number))
                throw new FormatException("У объекта OSM отсутствует корректный id.");
            string id = $"{type}/{number}";
            if (!identifiers.Add(id)) throw new FormatException($"Повторяющийся объект {id}.");
            if (type == "way" && consumedWays.Contains(number)) continue;
            try
            {
                if (tags.TryGetValue("building:part", out var part) && part != "no")
                    throw new ArgumentException("Отдельные building:part пока не поддерживаются; используется полный контур здания.");
                List<Polygon2D> polygons;
                var memberIds = new List<long>();
                if (type == "relation")
                {
                    if (!tags.TryGetValue("type", out var relationType) || relationType != "multipolygon")
                        throw new ArgumentException("Поддерживаются только отношения type=multipolygon.");
                    var outerSegments = new List<List<GeoPoint>>();
                    var innerSegments = new List<List<GeoPoint>>();
                    if (!element.TryGetProperty("members", out var members) || members.ValueKind != JsonValueKind.Array)
                        throw new ArgumentException("Не указаны members мультиполигона.");
                    foreach (var member in members.EnumerateArray())
                    {
                        string role = Text(member, "role");
                        if (Text(member, "type") != "way" || role is not ("outer" or "inner" or ""))
                            throw new ArgumentException("Неподдерживаемый участник мультиполигона.");
                        (role == "inner" ? innerSegments : outerSegments).Add(ReadGeometry(member));
                        memberIds.Add(member.GetProperty("ref").GetInt64());
                    }
                    var outers = JoinRings(outerSegments).Select(r => new Polygon2D(ProjectRing(r, projector))).ToArray();
                    if (outers.Length == 0) throw new ArgumentException("Нет внешнего контура.");
                    var holes = JoinRings(innerSegments).Select(r => ProjectRing(r, projector)).ToArray();
                    var assignments = outers.Select(_ => new List<IReadOnlyList<Point2D>>()).ToArray();
                    foreach (var hole in holes)
                    {
                        var owners = Enumerable.Range(0, outers.Length).Where(i => outers[i].Contains(hole[0], false)).ToArray();
                        if (owners.Length != 1) throw new ArgumentException("Нельзя однозначно отнести двор к внешнему контуру.");
                        assignments[owners[0]].Add(hole);
                    }
                    polygons = outers.Select((p, i) => new Polygon2D(p.Outer, assignments[i])).ToList();
                }
                else
                {
                    var ring = ReadGeometry(element);
                    if (ring.Count < 4 || ring[0] != ring[^1]) throw new ArgumentException("Контур здания не замкнут.");
                    polygons = [new Polygon2D(ProjectRing(ring, projector))];
                }

                BuildingHeight height;
                try { height = new BuildingHeightResolver().Resolve(tags, options, issues, id); }
                catch (ArgumentException error) when (options.Mode == SceneGenerator.Core.Models.SceneMode.TwoD)
                {
                    issues.Add($"{id}: высота не распознана ({error.Message}); контур доступен в 2D.");
                    height = new(options.DefaultHeight, 0, HeightOrigin.Default);
                }
                string name = tags.GetValueOrDefault("name", tags.GetValueOrDefault("addr:street", "Здание"));
                int partIndex = 0;
                foreach (Polygon2D polygon in polygons)
                {
                    string partId = polygons.Count == 1 ? id : $"{id}:{++partIndex}";
                    if (polygon.MinX < 0 || polygon.MinY < 0 || polygon.MaxX > options.Length || polygon.MaxY > options.Width)
                    {
                        issues.Add($"{partId}: здание выходит за границы участка и не включено. Расширьте или сместите область.");
                        continue;
                    }
                    buildings.Add(new(partId, name, polygon, height));
                    if (buildings.Count > 5000) throw new FormatException("Найдено более 5000 зданий. Уменьшите область.");
                }
                consumedWays.UnionWith(memberIds);
                if (polygons.Count > 1) issues.Add($"{id}: раздельные внешние контуры представлены отдельными объектами.");
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                issues.Add($"{id}: {error.Message}");
            }
        }
        return new(options, buildings.AsReadOnly(), issues.AsReadOnly(), data.Json, data.Source, data.LoadedAt, data.SavedProject);
    }

    private static Dictionary<string, string> Tags(JsonElement element) =>
        element.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object
            ? tags.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString()!) : new();

    private static string Text(JsonElement element, string key) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : "";

    private static List<GeoPoint> ReadGeometry(JsonElement element)
    {
        if (!element.TryGetProperty("geometry", out var geometry) || geometry.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Нет geometry; загрузите ответ с out body geom.");
        if (geometry.GetArrayLength() > 2000) throw new ArgumentException("Контур содержит более 2000 вершин.");
        var result = new List<GeoPoint>();
        foreach (var point in geometry.EnumerateArray())
        {
            if (point.ValueKind != JsonValueKind.Object || !point.TryGetProperty("lat", out var lat) ||
                !point.TryGetProperty("lon", out var lon) || !lat.TryGetDouble(out double latitude) || !lon.TryGetDouble(out double longitude) ||
                !double.IsFinite(latitude) || Math.Abs(latitude) > 90 || !double.IsFinite(longitude) || Math.Abs(longitude) > 180)
                throw new ArgumentException("Неполные или неверные координаты контура.");
            var p = new GeoPoint(latitude, longitude);
            if (result.Count == 0 || result[^1] != p) result.Add(p);
        }
        if (result.Count < 2) throw new ArgumentException("Недостаточно точек контура.");
        return result;
    }

    private static List<List<GeoPoint>> JoinRings(List<List<GeoPoint>> segments)
    {
        var pending = segments.Select(s => new List<GeoPoint>(s)).ToList();
        var result = new List<List<GeoPoint>>();
        while (pending.Count > 0)
        {
            var ring = pending[0]; pending.RemoveAt(0);
            while (ring[0] != ring[^1])
            {
                var matches = pending.Select((s, i) => (s, i)).Where(x => x.s[0] == ring[^1] || x.s[^1] == ring[^1]).ToArray();
                if (matches.Length != 1) throw new ArgumentException("Контур мультиполигона разорван или неоднозначен.");
                var (segment, index) = matches[0]; pending.RemoveAt(index);
                if (segment[^1] == ring[^1]) segment.Reverse();
                ring.AddRange(segment.Skip(1));
                if (ring.Count > 2000) throw new ArgumentException("Составной контур содержит более 2000 вершин.");
            }
            result.Add(ring);
        }
        return result;
    }

    private static IReadOnlyList<Point2D> ProjectRing(List<GeoPoint> ring, ICoordinateProjector projector)
    {
        if (ring.Count < 4 || ring[0] != ring[^1]) throw new ArgumentException("Контур не замкнут.");
        return ring.Take(ring.Count - 1).Select(p => projector.Project(p.Latitude, p.Longitude)).ToArray();
    }

    private sealed record GeoPoint(double Latitude, double Longitude);
}
