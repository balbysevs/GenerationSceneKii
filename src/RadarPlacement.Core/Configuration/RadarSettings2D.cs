namespace RadarPlacement.Core.Configuration;

/// <summary>Общие параметры одинаковых всенаправленных радаров.</summary>
public sealed record RadarSettings2D
{
    /// <summary>
    /// Требуемое количество радаров, от 1 до 10 включительно. Результат содержит ровно
    /// столько установок, даже если дополнительные радары не увеличивают покрытие.
    /// </summary>
    public int RadarCount { get; init; } = 1;

    /// <summary>
    /// Радиус действия каждого радара в метрах. Должен быть положительным и конечным.
    /// Диапазон 3000–7000 м из ТЗ сможет проверять слой настроек приложения;
    /// вычислительное ядро допускает и другие радиусы для самостоятельного использования.
    /// </summary>
    public double Radius { get; init; } = 3000;

    internal void Validate()
    {
        if (RadarCount is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(RadarCount), "Количество радаров должно быть от 1 до 10.");
        if (!double.IsFinite(Radius) || Radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(Radius), "Радиус должен быть положительным и конечным.");
    }
}
