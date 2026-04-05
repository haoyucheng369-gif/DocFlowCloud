using DocFlowCloud.Infrastructure;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.NotificationService;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using DocFlowCloud.Application.Abstractions.Observability;

// NotificationService 入口：
// 它是独立消费者，只负责订阅通知类事件并执行通知副作用。
// 当前开始支持按 Messaging.Provider 在 RabbitMQ 和 Service Bus 之间切换。
var loggerConfiguration = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/notification-.log",
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
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("DocFlowCloud.NotificationService"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(DocFlowCloudTracing.SourceName)
            .AddHttpClientInstrumentation();

        if (!IsCloudEnvironment())
        {
            tracing.AddConsoleExporter();
        }
    });

// 复用同一套 Infrastructure，再补充通知发送器。
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<NotificationEmailSender>();

var messagingSettings = builder.Configuration
    .GetSection(MessagingSettings.SectionName)
    .Get<MessagingSettings>() ?? new MessagingSettings();

if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<ServiceBusNotificationWorker>();
}
else
{
    builder.Services.AddHostedService<NotificationWorker>();
}

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
