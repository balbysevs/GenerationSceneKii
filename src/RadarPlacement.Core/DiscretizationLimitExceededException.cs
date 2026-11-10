namespace RadarPlacement.Core;

/// <summary>
/// Выбранная дискретизация превышает явно заданный лимит ресурсов.
/// Приложение может предложить увеличить шаг сетки либо осознанно поднять лимит.
/// </summary>
public sealed class DiscretizationLimitExceededException : InvalidOperationException
{
    /// <summary>Создаёт исключение с объяснением превышенного ограничения.</summary>
    public DiscretizationLimitExceededException(string message) : base(message) { }
}
