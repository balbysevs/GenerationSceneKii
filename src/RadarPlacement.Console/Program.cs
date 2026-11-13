using System.Text;

namespace RadarPlacement.ConsoleApp;

/// <summary>Точка запуска консольного приложения. Вычисления и JSON остаются в отдельных библиотеках.</summary>
public static class Program
{
    /// <summary>
    /// Запускает русское интерактивное меню. --help выводит справку.
    /// Код 0 означает обычный выход, 1 — непредвиденную ошибку, 2 — неверные аргументы.
    /// Ctrl+C во время операции отменяет её; вне операции завершает приложение.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        System.Console.InputEncoding = new UTF8Encoding(false);
        System.Console.OutputEncoding = new UTF8Encoding(false);
        using var cancellation = new OperationCancellation();
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            // Обработчик сигнала только запрашивает отмену. Он не меняет список сцен,
            // не пишет файлы и не выводит текст одновременно с рабочей операцией.
            e.Cancel = cancellation.RequestCancellation();
        };
        System.Console.CancelKeyPress += onCancel;
        // Управление курсором предназначено только для окна. Справка, конвейеры и файлы
        // получают чистый текст без управляющих последовательностей и дополнительных пауз.
        IConsoleTerminal? terminal = args.Length == 0 && !System.Console.IsInputRedirected && !System.Console.IsOutputRedirected
            ? new ConsoleTerminal() : null;
        try
        {
            var app = new ConsoleApplication(System.Console.In, System.Console.Out,
                Directory.GetCurrentDirectory(), Path.Combine(AppContext.BaseDirectory, "Demo"), cancellation, terminal);
            return await app.RunAsync(args);
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine("Программа завершена из-за непредвиденной ошибки: " + ConsoleUi.Safe(exception.Message));
            return 1;
        }
        finally
        {
            System.Console.CancelKeyPress -= onCancel;
            if (terminal is ConsoleTerminal window) window.RestoreCursor();
        }
    }
}
