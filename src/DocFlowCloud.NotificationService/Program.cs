using DocFlowCloud.Infrastructure;
using DocFlowCloud.NotificationService;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<NotificationEmailSender>();
builder.Services.AddHostedService<NotificationWorker>();

builder.Services.AddSerilog();

var host = builder.Build();
host.Run();
