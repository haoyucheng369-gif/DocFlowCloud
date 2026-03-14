using DocFlowCloud.Infrastructure;
using DocFlowCloud.NotificationService;
using Microsoft.Extensions.Hosting;
using Serilog;

// NotificationService 入口：
// 它是独立消费者，只负责订阅通知事件并执行通知副作用。
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/notification-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

// 复用同一套 Infrastructure，自己额外注册通知发送器。
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<NotificationEmailSender>();
builder.Services.AddHostedService<NotificationWorker>();

// 统一使用 Serilog 输出日志。
builder.Services.AddSerilog();

var host = builder.Build();
host.Run();
