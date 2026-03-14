using DocFlowCloud.Application.Abstractions.Messaging;
using DocFlowCloud.Application.Abstractions.Persistence;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.Infrastructure.Persistence;
using DocFlowCloud.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocFlowCloud.Infrastructure;

// Infrastructure 注册入口：
// 统一把数据库、RabbitMQ 配置和各类仓储/发布器注入到 DI 容器里。
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 当前项目使用 SQL Server 作为主数据库。
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // RabbitMQ 配置走 Options 风格读取，这里直接绑定成单例对象。
        var rabbitMqSettings = configuration
            .GetSection(RabbitMqSettings.SectionName)
            .Get<RabbitMqSettings>() ?? new RabbitMqSettings();

        services.AddSingleton(rabbitMqSettings);
        // 这些注册把应用层接口接到基础设施实现上。
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobMessagePublisher, RabbitMqJobMessagePublisher>();
        services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();

        return services;
    }
}
