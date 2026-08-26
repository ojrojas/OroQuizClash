namespace BuildingBlocks.EventBus.RabbitMQ;

public interface IRabbitMqConnection : IAsyncDisposable
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Lazily opens and caches a single AMQP connection for the process.
/// The client's automatic recovery keeps it alive across broker restarts.
/// </summary>
public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnection> logger) : IRabbitMqConnection
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                UserName = settings.UserName,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true
            };

            var attempt = 0;
            while (true)
            {
                try
                {
                    if (_connection is not null)
                    {
                        await _connection.DisposeAsync().ConfigureAwait(false);
                    }

                    _connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
                    logger.LogInformation("Connected to RabbitMQ at {HostName}:{Port}", settings.HostName, settings.Port);
                    return _connection;
                }
                catch (Exception exception) when (attempt < settings.RetryCount)
                {
                    attempt++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    logger.LogWarning(exception,
                        "Could not connect to RabbitMQ (attempt {Attempt}/{RetryCount}). Retrying in {Delay}s",
                        attempt, settings.RetryCount, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        _connectionLock.Dispose();
    }
}