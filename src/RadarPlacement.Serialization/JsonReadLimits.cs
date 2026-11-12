namespace RadarPlacement.Serialization;

/// <summary>Ограничения входного документа, проверяемые до создания больших моделей.</summary>
public sealed record JsonReadLimits
{
    /// <summary>Максимальный размер UTF-8-документа, по умолчанию 16 МиБ.</summary>
    public int MaxBytes { get; init; } = 16 * 1024 * 1024;

    /// <summary>Максимальная глубина вложенности JSON, по умолчанию 64 уровня.</summary>
    public int MaxDepth { get; init; } = 64;

    internal void Validate()
    {
        if (MaxBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxBytes));
        if (MaxDepth is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(MaxDepth));
    }
}
