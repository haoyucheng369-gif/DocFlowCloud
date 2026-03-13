using DocFlowCloud.Application.Abstractions.Processing;
using DocFlowCloud.Infrastructure;
using DocFlowCloud.Worker;
using Microsoft.Extensions.Hosting;
using Serilog;

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

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IJobSideEffectExecutor, JobSideEffectExecutor>();

builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<RabbitMqWorker>();

builder.Services.AddSerilog();

var host = builder.Build();
host.Run();
