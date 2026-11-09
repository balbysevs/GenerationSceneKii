using System.Globalization;
using System.Text.RegularExpressions;

namespace SceneGenerator.OpenStreetMap;

public sealed partial class BuildingHeightResolver
{
    /// <summary>Определяет отметку основания и верхнюю отметку здания из тегов OSM или оценок, записывая замечания об исходных данных.</summary>
    public BuildingHeight Resolve(IReadOnlyDictionary<string, string> tags, OsmImportOptions options,
        ICollection<string> issues, string id)
    {
        double bottom = 0;
        if (tags.TryGetValue("min_height", out string? min))
        {
            if (!TryMeters(min, out bottom) || bottom < 0)
                throw new ArgumentException("Некорректная отметка min_height.");
        }
        else if (tags.TryGetValue("building:min_level", out string? minLevel))
        {
            if (!TryNumber(minLevel, out double level) || level < 0)
                throw new ArgumentException("Некорректный building:min_level.");
            bottom = level * options.FloorHeight;
            issues.Add($"{id}: основание оценено по building:min_level.");
        }

        double top;
        HeightOrigin origin;
        if (tags.TryGetValue("height", out string? height) && TryMeters(height, out top) && top > 0)
            origin = HeightOrigin.OsmHeight;
        else
        {
            if (tags.ContainsKey("height")) issues.Add($"{id}: значение height не распознано; используется оценка.");
            if (tags.TryGetValue("building:levels", out string? levels) && TryNumber(levels, out double count) && count > 0)
            {
                top = count * options.FloorHeight;
                if (tags.TryGetValue("roof:height", out string? roof) && TryMeters(roof, out double roofHeight) && roofHeight >= 0)
                    top += roofHeight;
                else if (tags.TryGetValue("roof:levels", out string? roofLevels) && TryNumber(roofLevels, out double roofCount) && roofCount >= 0)
                    top += roofCount * options.FloorHeight;
                origin = HeightOrigin.OsmLevels;
            }
            else
            {
                top = options.DefaultHeight;
                origin = HeightOrigin.Default;
            }
        }
        if (!double.IsFinite(top) || top <= bottom || top > 1000)
            throw new ArgumentException("Высота здания должна превышать отметку основания и быть не более 1000 м.");
        return new(top, bottom, origin);
    }

    /// <summary>Разбирает конечное десятичное число с точкой или запятой независимо от культуры процесса.</summary>
    private static bool TryNumber(string value, out double number) =>
        double.TryParse(value.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    /// <summary>Разбирает значение длины и переводит его в метры; поддерживает метры и футы.</summary>
    public static bool TryMeters(string value, out double meters)
    {
        meters = 0;
        Match match = LengthPattern().Match(value.Trim());
        if (!match.Success || !TryNumber(match.Groups[1].Value, out double number)) return false;
        meters = number * (match.Groups[2].Value.ToLowerInvariant() is "ft" or "feet" or "'" ? 0.3048 : 1);
        return double.IsFinite(meters);
    }

    /// <summary>Возвращает выражение для распознавания числовой длины с необязательной единицей измерения.</summary>
    [GeneratedRegex(@"^([+-]?\d+(?:[.,]\d+)?)\s*(m|метр|метров|ft|feet|')?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LengthPattern();
}
