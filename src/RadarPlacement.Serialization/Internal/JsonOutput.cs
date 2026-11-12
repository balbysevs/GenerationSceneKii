using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace RadarPlacement.Serialization.Internal;

internal static class JsonOutput
{
    internal static string Serialize(Action<Utf8JsonWriter> write, bool indented)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = indented,
            // Сохраняем русский текст читаемым; стандартные правила экранирования JSON действуют.
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic)
        }))
            write(writer);
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
