using DocFlowCloud.Api.Contracts;
using DocFlowCloud.Infrastructure.Messaging;
using DocFlowCloud.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;

namespace DocFlowCloud.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// System API 控制器：
// 给前端返回当前 API 所在环境和所依赖基础设施的安全摘要，方便联调时快速确认数据来源。
public sealed class SystemController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly MessagingSettings _messagingSettings;
    private readonly ServiceBusSettings _serviceBusSettings;
    private readonly RabbitMqSettings _rabbitMqSettings;
    private readonly StorageSettings _storageSettings;

    public SystemController(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        MessagingSettings messagingSettings,
        ServiceBusSettings serviceBusSettings,
        RabbitMqSettings rabbitMqSettings,
        StorageSettings storageSettings)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _messagingSettings = messagingSettings;
        _serviceBusSettings = serviceBusSettings;
        _rabbitMqSettings = rabbitMqSettings;
        _storageSettings = storageSettings;
    }

    [HttpGet("environment")]
    public ActionResult<SystemEnvironmentDto> GetEnvironment()
    {
        // 这里只故意只暴露“环境识别信息”，不把完整连接串直接返回给前端。
        var (databaseServer, databaseName) = ReadDatabaseTarget(
            _configuration.GetConnectionString("DefaultConnection"));
        var (messagingProvider, messagingTarget) = ReadMessagingTarget();

        return Ok(new SystemEnvironmentDto
        {
            ApiEnvironment = _hostEnvironment.EnvironmentName,
            DatabaseServer = databaseServer,
            DatabaseName = databaseName,
            MessagingProvider = messagingProvider,
            MessagingTarget = messagingTarget,
            StorageProvider = _storageSettings.Provider
        });
    }

    private (string Provider, string Target) ReadMessagingTarget()
    {
        if (string.Equals(_messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            return ("ServiceBus", $"{_serviceBusSettings.TopicName} / {_serviceBusSettings.WorkerSubscriptionName}");
        }

        return ("RabbitMq", $"{_rabbitMqSettings.HostName} ({_rabbitMqSettings.VirtualHost})");
    }

    private static (string DatabaseServer, string DatabaseName) ReadDatabaseTarget(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ("Unknown", "Unknown");
        }

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        var databaseServer = ReadFirstValue(builder, "Data Source", "Server", "Addr", "Address", "Network Address");
        var databaseName = ReadFirstValue(builder, "Initial Catalog", "Database");

        return (
            string.IsNullOrWhiteSpace(databaseServer) ? "Unknown" : databaseServer,
            string.IsNullOrWhiteSpace(databaseName) ? "Unknown" : databaseName);
    }

    private static string? ReadFirstValue(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }
}
