namespace QuizArena.Admin.Client.Services;

public sealed class LiveGamePoller : IAsyncDisposable
{
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(4);

    public bool IsRunning => _timer is not null;

    public void Start(Func<CancellationToken, Task> pollAction)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(_interval);
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (await _timer.WaitForNextTickAsync(token))
                {
                    // Respect visibilityState if available via JS interop - caller should check
                    await pollAction(token);
                }
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _timer?.Dispose();
        _timer = null;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
