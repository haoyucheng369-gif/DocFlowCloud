using RabbitMQ.Client;

namespace DocFlowCloud.Infrastructure.Messaging;

// RabbitMQ 拓扑初始化器接口：
// 统一声明 exchange / queue / binding，避免各处重复维护一套 RabbitMQ 结构。
public interface IRabbitMqTopologyInitializer
{
    // 对当前 channel 补齐当前系统所需的所有拓扑结构。
    void EnsureTopology(IModel channel);
}
