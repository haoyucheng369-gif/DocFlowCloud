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

// API 进程入口：
// 负责启动 HTTP 服务、配置日志、注册中间件与依赖注入。
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

// 让 ASP.NET Core 的日志也统一走 Serilog。
builder.Host.UseSerilog();

// 注册 HTTP / API 相关服务。
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>();
builder.Services.AddScoped<ICorrelationContextAccessor, HttpCorrelationContextAccessor>();

// 基础健康检查，当前重点检查数据库是否可连通。
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DocFlowCloud.Infrastructure.Persistence.AppDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注入 Infrastructure 和应用服务。
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();

var app = builder.Build();

// 先挂链路追踪和统一异常处理中间件。
app.UseCorrelationIdMiddleware();
app.UseGlobalExceptionMiddleware();

// 常规 API 中间件。
app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
