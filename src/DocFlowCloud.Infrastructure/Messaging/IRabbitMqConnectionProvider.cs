using RabbitMQ.Client;

namespace DocFlowCloud.Infrastructure.Messaging;

// RabbitMQ 连接提供器接口：
// 统一向发布器和消费者提供可复用的长期连接。
public interface IRabbitMqConnectionProvider : IDisposable
{
    // 获取当前进程内可复用的 RabbitMQ 连接。
    IConnection GetConnection();
}
