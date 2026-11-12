using System.Text.Json;
using RadarPlacement.Core.Results;
using RadarPlacement.Serialization.Internal;

namespace RadarPlacement.Serialization;

/// <summary>
/// Экспортирует исходную сцену с дополнительными полями radars и solver.
/// Сохраняет все исходные поля и объекты, включая неизвестные метаданные.
/// Версия схемы генератора не меняется; версия нашего расширения находится в solver.schemaVersion.
/// </summary>
public sealed class ResultJsonWriter2D
{
    /// <summary>
    /// Формирует JSON результата. Результат должен быть получен вызовом Solve для того же
    /// экземпляра source.Scene: это предотвращает ошибочное соединение геометрии одной сцены
    /// с идентификаторами и метаданными другой. Карты покрытия добавляются по настройке options.
    /// </summary>
    public string Serialize(SceneDocument2D source, RadarPlacementResult2D result,
        ResultJsonOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);
        if (!ReferenceEquals(source.Scene, result.Scene))
            throw new ArgumentException("Результат должен быть рассчитан для source.Scene этого документа.", nameof(result));
        var settings = new SolverSettings2D { Radars = result.RadarSettings, Discretization = result.Discretization };
        SolverSettingsJson.Validate(settings);
        var actual = options ?? new();
        cancellationToken.ThrowIfCancellationRequested();
        string[] radarIds = CreateRadarIds(source, result.Radars.Count);
        return JsonOutput.Serialize(writer =>
        {
            writer.WriteStartObject();
            foreach (var property in source.OriginalJson.EnumerateObject()) property.WriteTo(writer);
            WriteRadars(writer, source.KiiObjectId, radarIds, result);
            writer.WriteStartObject("solver");
            writer.WriteString("schemaVersion", "1.0");
            writer.WriteString("algorithm", "branchAndBound");
            writer.WriteString("objective", "coveredArea2D");
            writer.WriteString("coverageModel", "uniformCellCenters");
            writer.WriteBoolean("globallyOptimalForDiscreteModel", result.IsGloballyOptimalForDiscreteModel);
            writer.WriteBoolean("kiiBlocksVisibility", false);
            writer.WriteBoolean("obstaclesBlockVisibility", true);
            writer.WriteBoolean("obstacleBoundaryBlocksVisibility", true);
            writer.WriteBoolean("coLocatedRadarsAllowed", true);
            writer.WritePropertyName("settings");
            SolverSettingsJson.Write(writer, settings);
            writer.WriteStartObject("search");
            writer.WriteNumber("candidateCount", result.CandidatePositions.Count);
            writer.WriteNumber("visitedNodes", result.VisitedSearchNodes);
            writer.WriteNumber("prunedNodes", result.PrunedSearchNodes);
            writer.WriteEndObject();
            WriteMetrics(writer, result.Coverage);
            if (actual.IncludeCoverageGrid) WriteGrid(writer, result, radarIds, cancellationToken);
            writer.WriteEndObject();
            writer.WriteEndObject();
            cancellationToken.ThrowIfCancellationRequested();
        }, actual.WriteIndented);
    }

    /// <summary>Сохраняет результат в UTF-8-файл. Существующие файлы по умолчанию не заменяются.</summary>
    public void Save(SceneDocument2D source, RadarPlacementResult2D result, string path,
        ResultJsonOptions? options = null, bool overwrite = false) =>
        SaveAsync(source, result, path, options, overwrite).GetAwaiter().GetResult();

    /// <summary>Сохраняет полностью сформированный результат через временный файл, с поддержкой отмены.</summary>
    public Task SaveAsync(SceneDocument2D source, RadarPlacementResult2D result, string path,
        ResultJsonOptions? options = null, bool overwrite = false, CancellationToken cancellationToken = default) =>
        JsonFiles.WriteAsync(path, Serialize(source, result, options, cancellationToken), overwrite, cancellationToken);

    private static string[] CreateRadarIds(SceneDocument2D source, int count)
    {
        var used = source.OriginalJson.GetProperty("objects").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
        var ids = new string[count];
        int sequence = 1;
        for (int i = 0; i < count; i++)
        {
            string id;
            do { id = $"radar-{sequence++:D3}"; } while (!used.Add(id));
            ids[i] = id;
        }
        return ids;
    }

    private static void WriteRadars(Utf8JsonWriter writer, string kiiId, string[] ids, RadarPlacementResult2D result)
    {
        writer.WriteStartArray("radars");
        for (int i = 0; i < result.Radars.Count; i++)
        {
            var radar = result.Radars[i];
            writer.WriteStartObject();
            writer.WriteString("id", ids[i]);
            writer.WriteString("objectId", kiiId);
            writer.WriteStartObject("position");
            writer.WriteNumber("x", radar.Position.X);
            writer.WriteNumber("y", radar.Position.Y);
            writer.WriteNumber("z", 0);
            writer.WriteEndObject();
            writer.WriteNumber("radius", radar.Radius);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteMetrics(Utf8JsonWriter writer, CoverageMap2D map)
    {
        writer.WriteStartObject("coverage");
        writer.WriteString("areaUnits", "squareMeters");
        writer.WriteNumber("columns", map.Columns);
        writer.WriteNumber("rows", map.Rows);
        writer.WriteNumber("cellWidth", map.CellWidth);
        writer.WriteNumber("cellHeight", map.CellHeight);
        writer.WriteNumber("cellArea", map.CellArea);
        writer.WriteNumber("observableCellCount", map.ObservableCellCount);
        writer.WriteNumber("coveredCellCount", map.CoveredCellCount);
        writer.WriteNumber("observableArea", map.ObservableArea);
        writer.WriteNumber("coveredArea", map.CoveredArea);
        if (map.CoveragePercent is double percent) writer.WriteNumber("coveragePercent", percent);
        else writer.WriteNull("coveragePercent");
        writer.WriteEndObject();
    }

    private static void WriteGrid(Utf8JsonWriter writer, RadarPlacementResult2D result, string[] ids, CancellationToken token)
    {
        var map = result.Coverage;
        int cellCount = checked(map.Columns * map.Rows);
        int byteCount = (int)(((long)cellCount + 7) / 8);
        byte[] excluded = new byte[byteCount], covered = new byte[byteCount];
        var individual = Enumerable.Range(0, ids.Length).Select(_ => new byte[byteCount]).ToArray();
        for (int row = 0; row < map.Rows; row++)
            for (int column = 0; column < map.Columns; column++)
            {
                int index = row * map.Columns + column;
                if ((index & 255) == 0) token.ThrowIfCancellationRequested();
                byte bit = (byte)(1 << (index & 7));
                var state = map.GetCellState(column, row);
                if (state == CoverageCellState2D.Excluded) excluded[index >> 3] |= bit;
                if (state == CoverageCellState2D.Covered) covered[index >> 3] |= bit;
                for (int radar = 0; radar < ids.Length; radar++)
                    if (map.IsCoveredByRadar(radar, column, row)) individual[radar][index >> 3] |= bit;
            }
        writer.WriteStartObject("coverageGrid");
        writer.WriteString("encoding", "base64-lsb0");
        writer.WriteString("indexOrder", "row-major");
        writer.WriteString("rowDirection", "south-to-north");
        writer.WriteBase64String("excludedMask", excluded);
        writer.WriteBase64String("coveredMask", covered);
        writer.WriteStartArray("radarMasks");
        for (int i = 0; i < ids.Length; i++)
        {
            writer.WriteStartObject();
            writer.WriteString("radarId", ids[i]);
            writer.WriteBase64String("coveredMask", individual[i]);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
