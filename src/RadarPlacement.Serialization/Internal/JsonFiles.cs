using System.Text;

namespace RadarPlacement.Serialization.Internal;

/// <summary>Ограниченное чтение UTF-8 и публикация полностью записанного файла.</summary>
internal static class JsonFiles
{
    internal static async Task<string> ReadAsync(string path, JsonReadLimits? limits, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var actual = limits ?? new();
        actual.Validate();
        token.ThrowIfCancellationRequested();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            16384, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > actual.MaxBytes) throw TooLarge(actual);
        using var buffer = new MemoryStream();
        byte[] block = new byte[16384];
        int read;
        while ((read = await stream.ReadAsync(block, token).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + read > actual.MaxBytes) throw TooLarge(actual);
            buffer.Write(block, 0, read);
        }
        try
        {
            // Строгая кодировка не заменяет повреждённые байты молча символом '?'.
            return new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
        catch (DecoderFallbackException exception)
        {
            throw new RadarJsonException("$", "Файл должен содержать корректный UTF-8.", exception);
        }
    }

    internal static async Task WriteAsync(string path, string json, bool overwrite, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        token.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            // Временный файл лежит в той же папке. Исходный результат не меняется,
            // пока новая версия не записана целиком и запрос отмены не проверен.
            await File.WriteAllTextAsync(temporaryPath, json + Environment.NewLine, new UTF8Encoding(false), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static RadarJsonException TooLarge(JsonReadLimits limits) =>
        new("$", $"Размер файла превышает {limits.MaxBytes} байт.");
}
