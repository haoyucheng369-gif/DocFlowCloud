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
    private readonly RabbitMqSettings _rabbitMqSettings;
    private readonly StorageSettings _storageSettings;

    public SystemController(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        RabbitMqSettings rabbitMqSettings,
        StorageSettings storageSettings)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _rabbitMqSettings = rabbitMqSettings;
        _storageSettings = storageSettings;
    }

    [HttpGet("environment")]
    public ActionResult<SystemEnvironmentDto> GetEnvironment()
    {
        // 这里故意只暴露“环境识别信息”，不把完整连接串直接返回给前端。
        var (databaseServer, databaseName) = ReadDatabaseTarget(
            _configuration.GetConnectionString("DefaultConnection"));

        return Ok(new SystemEnvironmentDto
        {
            ApiEnvironment = _hostEnvironment.EnvironmentName,
            DatabaseServer = databaseServer,
            DatabaseName = databaseName,
            RabbitMqHost = _rabbitMqSettings.HostName,
            StorageProvider = _storageSettings.Provider
        });
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
