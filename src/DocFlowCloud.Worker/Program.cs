using DocFlowCloud.Application.Abstractions.Processing;
using DocFlowCloud.Infrastructure;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.Worker;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;

// Worker 进程入口：
// 当前开始支持“本地 RabbitMQ / testbed ServiceBus”双 provider。
// 先保留 OutboxPublisherWorker 和 StaleInboxRecoveryWorker，
// 再按 Messaging.Provider 决定到底启动 RabbitMqWorker 还是 ServiceBusWorker。
QuestPDF.Settings.License = LicenseType.Community;

var loggerConfiguration = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/worker-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}");

if (IsCloudEnvironment())
{
    loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
}
else
{
    loggerConfiguration.WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}");
}

Log.Logger = loggerConfiguration.CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IJobSideEffectExecutor, JobSideEffectExecutor>();

var messagingSettings = builder.Configuration
    .GetSection(MessagingSettings.SectionName)
    .Get<MessagingSettings>() ?? new MessagingSettings();

builder.Services.AddHostedService<OutboxPublisherWorker>();

// 这里是 worker 消费端的切换点：
// - Development 继续 RabbitMqWorker，方便本地调试
// - Testbed / Production 切 ServiceBusWorker，走云上消息总线
if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<ServiceBusWorker>();
}
else
{
    builder.Services.AddHostedService<RabbitMqWorker>();
}

builder.Services.AddHostedService<StaleInboxRecoveryWorker>();
builder.Services.AddSerilog();

var host = builder.Build();
host.Run();

static bool IsCloudEnvironment()
{
    var environmentName =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
        string.Empty;

    return environmentName.Equals("Testbed", StringComparison.OrdinalIgnoreCase) ||
           environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);
}
