using RadarPlacement.Core;
using RadarPlacement.Serialization;

namespace RadarPlacement.ConsoleApp;

/// <summary>Ошибки ввода, файлов и настроенных лимитов возвращают пользователя в меню.</summary>
internal static class ConsoleErrors
{
    internal static bool IsExpected(Exception exception) => exception is
        IOException or UnauthorizedAccessException or RadarJsonException or
        ArgumentException or NotSupportedException or DiscretizationLimitExceededException;
}
