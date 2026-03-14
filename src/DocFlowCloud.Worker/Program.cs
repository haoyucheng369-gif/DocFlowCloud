using DocFlowCloud.Application.Abstractions.Processing;
using DocFlowCloud.Infrastructure;
using DocFlowCloud.Worker;
using Microsoft.Extensions.Hosting;
using Serilog;

// Worker 进程入口：
// 负责启动发送侧 worker、消费侧 worker 和卡死恢复 worker。
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

// 注入基础设施和外部副作用执行器。
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IJobSideEffectExecutor, JobSideEffectExecutor>();

// 发送、消费、恢复三个后台 worker 同时运行在这个进程里。
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<RabbitMqWorker>();
builder.Services.AddHostedService<StaleInboxRecoveryWorker>();

// 统一使用 Serilog 输出日志。
builder.Services.AddSerilog();

var host = builder.Build();
host.Run();
