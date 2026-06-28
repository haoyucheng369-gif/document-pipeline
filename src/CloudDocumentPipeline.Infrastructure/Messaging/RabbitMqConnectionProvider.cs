using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// RabbitMQ 闀胯繛鎺ユ彁渚涘櫒锛?
// 鏁翠釜杩涚▼灏介噺澶嶇敤鍚屼竴涓?IConnection锛岄渶瑕?channel 鏃跺啀鎸夐渶鍒涘缓銆?
public sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider
{
    private readonly ConnectionFactory _factory;
    private readonly object _syncRoot = new();
    private IConnection? _connection;
    private bool _disposed;

    public RabbitMqConnectionProvider(RabbitMqSettings settings)
    {
        // 杩欓噷鍙垵濮嬪寲杩炴帴宸ュ巶锛岀湡姝ｈ繛鎺ュ欢杩熷埌棣栨浣跨敤鏃跺啀鍒涘缓銆?
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

        // 杩炴帴浠嶇劧鍙敤鏃剁洿鎺ュ鐢紝閬垮厤姣忔閮介噸鏂板缓绔?TCP 杩炴帴銆?
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        // 杩炴帴鍒涘缓杩囩▼鍔犻攣锛岄伩鍏嶅苟鍙戜笅寤虹珛鍑哄涓暱杩炴帴銆?
        lock (_syncRoot)
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

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

            // 瀹夸富閫€鍑烘椂缁熶竴閲婃斁闀胯繛鎺ャ€?
            _connection?.Close();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
