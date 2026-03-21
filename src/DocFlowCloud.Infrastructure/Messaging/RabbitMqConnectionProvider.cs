using RabbitMQ.Client;

namespace DocFlowCloud.Infrastructure.Messaging;

// RabbitMQ 长连接提供器：
// 整个进程尽量复用同一个 IConnection，需要 channel 时再按需创建。
public sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider
{
    private readonly ConnectionFactory _factory;
    private readonly object _syncRoot = new();
    private IConnection? _connection;
    private bool _disposed;

    public RabbitMqConnectionProvider(RabbitMqSettings settings)
    {
        // 这里只初始化连接工厂，真正连接延迟到首次使用时再创建。
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

        // 连接仍然可用时直接复用，避免每次都重新建立 TCP 连接。
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        // 连接创建过程加锁，避免并发下建立出多个长连接。
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

            // 宿主退出时统一释放长连接。
            _connection?.Close();
            _connection?.Dispose();
            _disposed = true;
        }
    }
}
