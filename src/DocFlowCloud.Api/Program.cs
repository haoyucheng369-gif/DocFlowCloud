using DocFlowCloud.Api.Extensions;
using DocFlowCloud.Api.Observability;
using DocFlowCloud.Api.Realtime;
using DocFlowCloud.Api.Validators;
using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Infrastructure;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

const string FrontendCorsPolicy = "FrontendCorsPolicy";

// API 进程入口：
// 负责组装 HTTP API、SignalR、日志、中间件、基础设施依赖和实时消息消费者。
// 当前在原有结构上补充了按 Messaging.Provider 在 RabbitMQ 和 Service Bus 之间切换实时消费者的能力。
var loggerConfiguration = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/api-.log",
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

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("DocFlowCloud.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(DocFlowCloudTracing.SourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!IsCloudEnvironment())
        {
            tracing.AddConsoleExporter();
        }
    });

// 注册 API 层常规能力：
// 控制器、SignalR、ProblemDetails、FluentValidation、CorrelationId 访问器。
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
{
    // 调试 SignalR 时，后端如果因为断点暂停较久，默认超时会比较容易断线。
    // 这里把服务端超时窗口放宽，减少调试阶段的假性断连。
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
});
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>();
builder.Services.AddScoped<ICorrelationContextAccessor, HttpCorrelationContextAccessor>();

// 当前项目主要用于本地开发和测试，前端来源暂时全部放开。
// 后续上 testbed / production 时，可以改回按域名白名单放行。
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 基础健康检查：
// - /health/live 只判断进程本身是否还活着
// - /health/ready 判断应用是否已经准备好提供服务，当前至少包括数据库可连接
builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: ["live"])
    .AddDbContextCheck<AppDbContext>(tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册基础设施和应用服务。
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();

// 这个后台消费者负责订阅状态变化事件，
// 再把状态变化转成 SignalR 推送给前端。
// 当前先保留总开关，再按 Messaging.Provider 决定启动 RabbitMQ 版还是 Service Bus 版消费者。
var enableJobStatusConsumer =
    builder.Configuration.GetValue("Realtime:EnableJobStatusConsumer", true);

var messagingSettings = builder.Configuration
    .GetSection(MessagingSettings.SectionName)
    .Get<MessagingSettings>() ?? new MessagingSettings();

if (enableJobStatusConsumer)
{
    if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddHostedService<ServiceBusJobStatusUpdatesConsumer>();
    }
    else
    {
        builder.Services.AddHostedService<JobStatusUpdatesConsumer>();
    }
}

var app = builder.Build();

// 先挂链路追踪和全局异常处理中间件，保证后续日志和错误输出统一。
app.UseCorrelationIdMiddleware();
app.UseGlobalExceptionMiddleware();

app.UseSerilogRequestLogging();
app.UseCors(FrontendCorsPolicy);
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new()
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
// 前端通过这个 Hub 订阅 Job 状态更新。
app.MapHub<JobUpdatesHub>("/hubs/jobs");

app.Run();

static bool IsCloudEnvironment()
{
    var environmentName =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
        string.Empty;

    return environmentName.Equals("Testbed", StringComparison.OrdinalIgnoreCase) ||
           environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);
}

public partial class Program;
