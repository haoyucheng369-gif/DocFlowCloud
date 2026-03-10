using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Infrastructure;
using DocFlowCloud.Worker;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();
builder.Services.AddHostedService<RabbitMqWorker>();

var host = builder.Build();
host.Run();