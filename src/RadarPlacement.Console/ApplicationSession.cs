using RadarPlacement.Core.Results;
using RadarPlacement.Serialization;

namespace RadarPlacement.ConsoleApp;

/// <summary>
/// Состояние одного сеанса консоли. Изменение настроек не уничтожает прошлые результаты:
/// они остаются доступны с пометкой, что рассчитаны с другими параметрами.
/// Файлы на диске при изменении списка сцен никогда не удаляются.
/// </summary>
internal sealed class ApplicationSession(string workingDirectory)
{
    internal List<SceneEntry> Scenes { get; } = [];
    /// <summary>Последняя загруженная, выбранная или обрабатываемая сцена для постоянной сводки.</summary>
    internal SceneEntry? CurrentScene { get; set; }
    internal SolverSettings2D Settings { get; private set; } = new();
    internal string OutputDirectory { get; set; } = Path.Combine(workingDirectory, "Result");
    internal bool IncludeCoverageGrid { get; set; }

    internal void SetSettings(SolverSettings2D settings)
    {
        SolverSettingsJson.Validate(settings);
        Settings = settings;
    }

    internal bool Contains(string fullPath) => Scenes.Any(s => string.Equals(s.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Снимок загруженного файла и последний успешно завершённый расчёт этой сцены.</summary>
internal sealed class SceneEntry(string sourcePath, SceneDocument2D document)
{
    internal string SourcePath { get; } = sourcePath;
    internal SceneDocument2D Document { get; } = document;
    internal RadarPlacementResult2D? Result { get; set; }
    internal string? SavedPath { get; set; }
    internal string? LastFailure { get; set; }
    internal TimeSpan CalculationTime { get; set; }
    internal bool UsesSettings(SolverSettings2D settings) => Result is not null &&
        Result.RadarSettings == settings.Radars && Result.Discretization == settings.Discretization;
}
