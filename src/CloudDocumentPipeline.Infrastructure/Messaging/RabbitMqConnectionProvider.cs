using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Lazily creates and reuses the RabbitMQ TCP connection for local broker operations.
public sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider
{
    private readonly ConnectionFactory _factory;
    private readonly object _syncRoot = new();
    private IConnection? _connection;
    private bool _disposed;

    public RabbitMqConnectionProvider(RabbitMqSettings settings)
    {
        _factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost
        };
    }

    public IConnection GetConnection()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMqConnectionProvider));
        }

        // Fast path avoids locking once the connection is healthy.
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        lock (_syncRoot)
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            // Replace stale connections so callers always receive an open connection.
            _connection?.Dispose();
            _connection = _factory.CreateConnection();
            return _connection;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            // Dispose owns the connection lifetime; individual publishers/consumers own channels.
            _connection?.Close();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
