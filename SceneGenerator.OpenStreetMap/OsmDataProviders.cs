using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SceneGenerator.OpenStreetMap;

public interface IOsmDataProvider
{
    Task<OsmData> LoadAsync(OsmImportOptions options, CancellationToken cancellationToken = default);
}

public sealed class FileOsmDataProvider(string path) : IOsmDataProvider
{
    public async Task<OsmData> LoadAsync(OsmImportOptions options, CancellationToken cancellationToken = default)
    {
        options.Validate();
        if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new IOException("Файл превышает лимит 64 МиБ.");
        string text = await File.ReadAllTextAsync(path, cancellationToken);
        var project = OsmImportProject.TryRead(text);
        return project is null ? new(text, Path.GetFullPath(path), DateTimeOffset.UtcNow)
            : new(project.RawOverpassJson, project.Source, project.LoadedAt, project);
    }
}

public sealed class OverpassDataProvider : IOsmDataProvider
{
    public static Uri DefaultEndpoint { get; } = new("https://maps.mail.ru/osm/tools/overpass/api/interpreter");
    public const int MaximumBytes = 32 * 1024 * 1024;
    private readonly HttpClient _client;
    private readonly Uri _endpoint;
    private readonly string? _cacheFolder;

    public OverpassDataProvider(HttpClient client, string? cacheFolder = null, Uri? endpoint = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _cacheFolder = cacheFolder;
        _endpoint = endpoint ?? DefaultEndpoint;
        if (_endpoint.Scheme != "https") throw new ArgumentException("Для Overpass требуется HTTPS.", nameof(endpoint));
    }

    public static string CreateQuery(OsmImportOptions options)
    {
        options.Validate();
        var b = options.GetQueryBounds();
        string box = string.Join(",", new[] { b.South, b.West, b.North, b.East }.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
        return $"[out:json][timeout:45];(way[\"building\"][\"building\"!=\"no\"]({box});relation[\"type\"=\"multipolygon\"][\"building\"][\"building\"!=\"no\"]({box}););out body geom;";
    }

    public async Task<OsmData> LoadAsync(OsmImportOptions options, CancellationToken cancellationToken = default)
    {
        string query = CreateQuery(options);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_endpoint + query))).ToLowerInvariant();
        string? cachePath = _cacheFolder is null ? null : Path.Combine(_cacheFolder, key + ".json");
        if (cachePath is not null && File.Exists(cachePath) && new FileInfo(cachePath).Length <= MaximumBytes &&
            DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < TimeSpan.FromDays(7))
        {
            string cached = await File.ReadAllTextAsync(cachePath, cancellationToken);
            try
            {
                ValidateResponse(cached);
                return new(cached, _endpoint + " (кэш)", File.GetLastWriteTimeUtc(cachePath));
            }
            catch (Exception error) when (error is FormatException or JsonException) {                                       }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(65));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            request.Headers.UserAgent.ParseAdd("SceneGeneratorKii/1.0");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query });
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.NotAcceptable)
                throw new HttpRequestException("Overpass временно ограничил запросы. Повторите позднее или откройте сохранённый ответ.");
            if (response.StatusCode is HttpStatusCode.GatewayTimeout or HttpStatusCode.ServiceUnavailable)
                throw new HttpRequestException("Сервер Overpass временно недоступен или перегружен. Повторите позднее, выберите другой сервер или откройте сохранённый JSON.");
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumBytes) throw new IOException("Ответ превышает 32 МиБ. Уменьшите область.");
            await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) != 0)
            {
                if (buffer.Length + read > MaximumBytes) throw new IOException("Ответ превышает 32 МиБ. Уменьшите область.");
                buffer.Write(chunk, 0, read);
            }
            string json = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
            ValidateResponse(json);
            if (cachePath is not null)
            {
                try
                {
                    Directory.CreateDirectory(_cacheFolder!);
                    TrimCache(_cacheFolder!);
                    string temporary = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        await File.WriteAllTextAsync(temporary, json, timeout.Token);
                        File.Move(temporary, cachePath, true);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) {                                            }
            }
            return new(json, _endpoint.ToString(), DateTimeOffset.UtcNow);
        }
        catch (HttpRequestException error) when (error.InnerException is System.Security.Authentication.AuthenticationException)
        {
            throw new HttpRequestException("Не удалось установить защищённое соединение с Overpass. Выберите другой сервер или откройте сохранённый JSON.", error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("Overpass не ответил вовремя. Повторите позднее или загрузите сохранённый JSON.");
        }
    }

    private static void TrimCache(string folder)
    {
        var files = new DirectoryInfo(folder).EnumerateFiles("*.json")
            .Where(f => f.Name.Length == 69 && f.Name[..64].All(Uri.IsHexDigit))
            .OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
        long total = 0;
        for (int i = 0; i < files.Length; i++)
        {
            total += files[i].Length;
            if (i >= 15 || total > 32L * 1024 * 1024 || DateTime.UtcNow - files[i].LastWriteTimeUtc > TimeSpan.FromDays(7))
                files[i].Delete();
        }
    }

    private static void ValidateResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || doc.RootElement.TryGetProperty("remark", out _) ||
            !doc.RootElement.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
            throw new FormatException("Сервер вернул ошибку или неполный ответ Overpass.");
    }
}
