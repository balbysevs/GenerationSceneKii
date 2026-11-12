using RadarPlacement.Core.Configuration;

namespace RadarPlacement.Serialization;

/// <summary>
/// Настройки приложения, которые можно загрузить из JSON или создать в интерфейсе.
/// JSON-слой проверяет диапазоны ТЗ: 1–10 радаров и радиус 3000–7000 м.
/// Обе вложенные модели непосредственно подходят методу Solve независимого ядра.
/// </summary>
public sealed record SolverSettings2D
{
    /// <summary>Количество радаров и одинаковый для всех радиус.</summary>
    public RadarSettings2D Radars { get; init; } = new();
    /// <summary>Шаги дискретизации и пределы ресурсов.</summary>
    public DiscretizationSettings2D Discretization { get; init; } = new();
}
