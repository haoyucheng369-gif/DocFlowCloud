using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Infrastructure;
using DocFlowCloud.Worker;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();

builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<RabbitMqWorker>();

builder.Services.AddSerilog();

var host = builder.Build();
host.Run();