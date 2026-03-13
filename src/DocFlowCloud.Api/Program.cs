using DocFlowCloud.Api.Extensions;
using DocFlowCloud.Api.Observability;
using DocFlowCloud.Api.Validators;
using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Infrastructure;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;
using DocFlowCloud.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 10,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [Corr:{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>();
builder.Services.AddScoped<ICorrelationContextAccessor, HttpCorrelationContextAccessor>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<DocFlowCloud.Infrastructure.Persistence.AppDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();

var app = builder.Build();

app.UseCorrelationIdMiddleware();
app.UseGlobalExceptionMiddleware();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
