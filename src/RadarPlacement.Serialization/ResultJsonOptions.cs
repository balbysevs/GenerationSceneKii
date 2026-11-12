namespace RadarPlacement.Serialization;

/// <summary>Параметры экспорта результата, не влияющие на расчёт.</summary>
public sealed record ResultJsonOptions
{
    /// <summary>
    /// Включать ли дискретные карты покрытия для повторной отрисовки без оптимизации.
    /// По умолчанию false: сохраняются позиции, параметры и показатели.
    /// Карты записываются компактными битовыми масками в Base64.
    /// </summary>
    public bool IncludeCoverageGrid { get; init; }
    /// <summary>Форматировать JSON отступами для чтения человеком, по умолчанию true.</summary>
    public bool WriteIndented { get; init; } = true;
}
