namespace RadarPlacement.Serialization;

/// <summary>
/// Ошибка синтаксиса, структуры или поддерживаемой модели JSON.
/// Ошибки файловой системы остаются обычными IOException/UnauthorizedAccessException,
/// чтобы приложение могло отличить неверные данные от недоступного файла.
/// </summary>
public sealed class RadarJsonException : FormatException
{
    /// <summary>Путь к проблемному значению, например $.objects[1].geometry.height.</summary>
    public string JsonPath { get; }

    /// <summary>Создаёт понятную ошибку данных с путём и необязательной исходной причиной.</summary>
    public RadarJsonException(string jsonPath, string message, Exception? innerException = null)
        : base($"{jsonPath}: {message}", innerException) => JsonPath = jsonPath;
}
