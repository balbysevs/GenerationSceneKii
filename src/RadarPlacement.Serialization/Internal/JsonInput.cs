using System.Text;
using System.Text.Json;

namespace RadarPlacement.Serialization.Internal;

/// <summary>Строгий разбор JSON и небольшие проверки с указанием пути ошибки.</summary>
internal static class JsonInput
{
    internal static JsonElement Parse(string json, JsonReadLimits? limits)
    {
        ArgumentNullException.ThrowIfNull(json);
        var actual = limits ?? new();
        actual.Validate();
        if (Encoding.UTF8.GetByteCount(json) > actual.MaxBytes)
            throw new RadarJsonException("$", $"Размер JSON превышает {actual.MaxBytes} байт.");
        // BOM допускается для файлов, сохранённых редактором Windows, но не является частью JSON.
        if (json.Length > 0 && json[0] == '\uFEFF') json = json[1..];
        try
        {
            using var document = JsonDocument.Parse(json, new() { MaxDepth = actual.MaxDepth });
            CheckDuplicates(document.RootElement, "$");
            return document.RootElement.Clone(); // Документ можно освободить, клон живёт самостоятельно.
        }
        catch (JsonException exception)
        {
            throw new RadarJsonException(exception.Path ?? "$", "Некорректный JSON: " + exception.Message, exception);
        }
    }

    private static void CheckDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                string childPath = path + "." + property.Name;
                if (!names.Add(property.Name))
                    throw new RadarJsonException(childPath, "Повторное имя свойства недопустимо.");
                CheckDuplicates(property.Value, childPath);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var child in element.EnumerateArray()) CheckDuplicates(child, $"{path}[{index++}]");
        }
    }

    internal static JsonElement Kind(JsonElement value, JsonValueKind kind, string path)
    {
        if (value.ValueKind != kind) throw new RadarJsonException(path, $"Ожидался тип {kind}.");
        return value;
    }

    internal static JsonElement Required(JsonElement parent, string name, string path)
    {
        Kind(parent, JsonValueKind.Object, path);
        if (!parent.TryGetProperty(name, out var value))
            throw new RadarJsonException(path + "." + name, "Обязательное поле отсутствует.");
        return value;
    }

    internal static JsonElement Object(JsonElement parent, string name, string path) =>
        Kind(Required(parent, name, path), JsonValueKind.Object, path + "." + name);

    internal static JsonElement Array(JsonElement parent, string name, string path) =>
        Kind(Required(parent, name, path), JsonValueKind.Array, path + "." + name);

    internal static string String(JsonElement parent, string name, string path)
    {
        string? text = Kind(Required(parent, name, path), JsonValueKind.String, path + "." + name).GetString();
        if (string.IsNullOrWhiteSpace(text)) throw new RadarJsonException(path + "." + name, "Строка не должна быть пустой.");
        return text;
    }

    internal static void Expect(JsonElement parent, string name, string expected, string path)
    {
        if (String(parent, name, path) != expected)
            throw new RadarJsonException(path + "." + name, $"Поддерживается только значение '{expected}'.");
    }

    internal static double Number(JsonElement parent, string name, string path)
    {
        var element = Required(parent, name, path);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out double value) || !double.IsFinite(value))
            throw new RadarJsonException(path + "." + name, "Требуется конечное число JSON, а не строка.");
        return value;
    }

    internal static long Integer(JsonElement parent, string name, string path)
    {
        var element = Required(parent, name, path);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out long value))
            throw new RadarJsonException(path + "." + name, "Требуется целое число в диапазоне Int64.");
        return value;
    }

    internal static void Zero(JsonElement parent, string name, string path)
    {
        if (Number(parent, name, path) != 0)
            throw new RadarJsonException(path + "." + name, "В режиме 2D отметки и высоты должны быть равны нулю.");
    }

    internal static void OnlyProperties(JsonElement value, string path, params string[] allowed)
    {
        Kind(value, JsonValueKind.Object, path);
        foreach (var property in value.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new RadarJsonException(path + "." + property.Name, "Неизвестная настройка; проверьте написание имени.");
    }
}
