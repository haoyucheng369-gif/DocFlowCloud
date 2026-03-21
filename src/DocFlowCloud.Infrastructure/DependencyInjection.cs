using DocFlowCloud.Application.Abstractions.Messaging;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Application.Abstractions.Storage;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.Infrastructure.Persistence;
using DocFlowCloud.Infrastructure.Persistence.Repositories;
using DocFlowCloud.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocFlowCloud.Infrastructure;

// Infrastructure 注册入口：
// 统一把数据库、RabbitMQ、文件存储和仓储实现注入到 DI 容器里。
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

        var storageSettings = configuration
            .GetSection(StorageSettings.SectionName)
            .Get<StorageSettings>() ?? new StorageSettings();

        services.AddSingleton(rabbitMqSettings);
        services.AddSingleton(storageSettings);

        // RabbitMQ 基础设施：
        // 连接提供器负责复用长连接，拓扑初始化器负责统一声明队列结构。
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();

        // 当前默认走 Local，共享目录适合本地联调；
        // 上云后把 Provider 切到 AzureBlob 即可。
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

        // 应用层仓储和消息发布器。
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobMessagePublisher, RabbitMqJobMessagePublisher>();
        services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();

        return services;
    }
}
