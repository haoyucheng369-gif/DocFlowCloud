using Azure.Messaging.ServiceBus;
using DocFlowCloud.Application.Abstractions.Messaging;
using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Abstractions.Storage;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.Infrastructure.Observability;
using DocFlowCloud.Infrastructure.Persistence;
using DocFlowCloud.Infrastructure.Persistence.Repositories;
using DocFlowCloud.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocFlowCloud.Infrastructure;

// Infrastructure 注入入口：
// 当前把“文件存储 provider”和“消息 provider”都集中在这里做配置切换。
// 这样应用层只依赖抽象接口，不直接关心 Local / AzureBlob / RabbitMq / ServiceBus 的具体实现。
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        var rabbitMqSettings = configuration
            .GetSection(RabbitMqSettings.SectionName)
            .Get<RabbitMqSettings>() ?? new RabbitMqSettings();

        var messagingSettings = configuration
            .GetSection(MessagingSettings.SectionName)
            .Get<MessagingSettings>() ?? new MessagingSettings();

        var serviceBusSettings = configuration
            .GetSection(ServiceBusSettings.SectionName)
            .Get<ServiceBusSettings>() ?? new ServiceBusSettings();

        var storageSettings = configuration
            .GetSection(StorageSettings.SectionName)
            .Get<StorageSettings>() ?? new StorageSettings();

        services.AddSingleton(rabbitMqSettings);
        services.AddSingleton(messagingSettings);
        services.AddSingleton(serviceBusSettings);
        services.AddSingleton(storageSettings);
        services.AddSingleton<IJobMetrics, JobMetrics>();

        // RabbitMQ 相关基础设施先继续注册：
        // 本地 Development 仍然需要 RabbitMQ 调试能力，等云上完全切完后再决定要不要进一步拆分注册。
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();

        if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(serviceBusSettings.ConnectionString))
            {
                throw new InvalidOperationException("ServiceBus provider requires a non-empty ServiceBus:ConnectionString.");
            }

            services.AddSingleton(_ => new ServiceBusClient(serviceBusSettings.ConnectionString));
        }

        if (string.Equals(storageSettings.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }
        else if (string.Equals(storageSettings.Provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported storage provider '{storageSettings.Provider}'. Supported values: Local, AzureBlob.");
        }

        services.AddScoped<IJobRepository, JobRepository>();

        // 发送侧先按 provider 切换：
        // 本地继续发 RabbitMQ，testbed/prod 切到 Service Bus。
        // 这样可以先完成“Outbox -> 消息总线”的迁移，再分阶段切消费者。
        if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IJobMessagePublisher, ServiceBusJobMessagePublisher>();
        }
        else
        {
            services.AddScoped<IJobMessagePublisher, RabbitMqJobMessagePublisher>();
        }

        services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();

        return services;
    }
}
