namespace AMS2CM.CLI;

internal sealed class ConsoleCancellation : IDisposable
{
    private readonly Lock gate = new();
    private readonly CancellationTokenSource source = new();
    private bool disposed;
    private readonly Action canceledAction;

    public CancellationToken Token { get; }

    public ConsoleCancellation(Action canceledAction)
    {
        Token = source.Token;
        Console.CancelKeyPress += CancelInstallation;
        this.canceledAction = canceledAction;
    }

    private void CancelInstallation(object? _, ConsoleCancelEventArgs e)
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            e.Cancel = true;

            if (source.IsCancellationRequested)
            {
                return;
            }

            source.Cancel();
            canceledAction();
        }
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= CancelInstallation;

        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            source.Dispose();
        }
    }
}
