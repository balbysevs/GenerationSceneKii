namespace RadarPlacement.ConsoleApp;

/// <summary>
/// Передаёт Ctrl+C только текущей операции. Каждый запуск получает новый токен:
/// отменённый расчёт не делает невозможным следующий. Lock защищает от гонки сигнала и Dispose.
/// </summary>
internal sealed class OperationCancellation : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _current;

    internal Lease Begin()
    {
        lock (_gate)
        {
            if (_current is not null) throw new InvalidOperationException("Операция уже выполняется.");
            _current = new CancellationTokenSource();
            return new Lease(this, _current);
        }
    }

    internal bool RequestCancellation()
    {
        lock (_gate)
        {
            if (_current is null) return false;
            _current.Cancel();
            return true;
        }
    }

    private void End(CancellationTokenSource source)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_current, source)) return;
            _current = null;
            source.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _current?.Dispose();
            _current = null;
        }
    }

    /// <summary>Область using отмечает точное время, когда Ctrl+C означает отмену операции.</summary>
    internal sealed class Lease(OperationCancellation owner, CancellationTokenSource source) : IDisposable
    {
        internal CancellationToken Token { get; } = source.Token;
        public void Dispose() => owner.End(source);
    }
}
