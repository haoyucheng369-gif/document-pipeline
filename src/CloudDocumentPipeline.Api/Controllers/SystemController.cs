using CloudDocumentPipeline.Api.Contracts;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;

namespace CloudDocumentPipeline.Api.Controllers;

// Exposes non-sensitive runtime configuration so the UI can show which backend it is using.
[ApiController]
[Route("api/[controller]")]
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
        // Only expose provider names and environment labels, never raw secret values.
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
