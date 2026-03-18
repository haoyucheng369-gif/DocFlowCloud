using DocFlowCloud.Api.Extensions;
using DocFlowCloud.Api.Observability;
using DocFlowCloud.Api.Realtime;
using DocFlowCloud.Api.Validators;
using DocFlowCloud.Application.Abstractions.Observability;
using DocFlowCloud.Application.Jobs;
using DocFlowCloud.Infrastructure;
using DocFlowCloud.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.AspNetCore;
using Serilog;

const string FrontendCorsPolicy = "FrontendCorsPolicy";

// API 进程入口：
// 负责组装 HTTP API、SignalR、日志、中间件、基础设施依赖和实时消息消费者。
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

// 基础健康检查，当前重点检查数据库是否能连接。
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册基础设施和应用服务。
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();

// 这个后台消费者负责订阅 RabbitMQ 上的 job.status.changed 事件，
// 再把状态变化转成 SignalR 推送给前端。
builder.Services.AddHostedService<JobStatusUpdatesConsumer>();

var app = builder.Build();

// 先挂链路追踪和全局异常处理中间件，保证后续日志和错误输出统一。
app.UseCorrelationIdMiddleware();
app.UseGlobalExceptionMiddleware();

app.UseSerilogRequestLogging();
app.UseCors(FrontendCorsPolicy);
app.MapHealthChecks("/health");
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// 前端通过这个 Hub 订阅 Job 状态更新。
app.MapHub<JobUpdatesHub>("/hubs/jobs");

app.Run();

public partial class Program;
