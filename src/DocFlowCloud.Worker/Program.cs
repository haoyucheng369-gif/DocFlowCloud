using DocFlowCloud.Application.Abstractions.Processing;
using DocFlowCloud.Infrastructure;
using DocFlowCloud.Worker;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;
using Serilog;

// Worker 进程入口：
// 负责启动发送侧、消费侧和卡死恢复这几个后台服务。
QuestPDF.Settings.License = LicenseType.Community;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/worker-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

// 注册基础设施和真正的副作用执行器。
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IJobSideEffectExecutor, JobSideEffectExecutor>();

// 三个后台服务：
// 1. OutboxPublisherWorker 扫描并发布消息
// 2. RabbitMqWorker 消费并处理任务
// 3. StaleInboxRecoveryWorker 自动接管卡死消息
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<RabbitMqWorker>();
builder.Services.AddHostedService<StaleInboxRecoveryWorker>();

// 统一使用 Serilog 输出控制台和文件日志。
builder.Services.AddSerilog();

var host = builder.Build();
host.Run();
