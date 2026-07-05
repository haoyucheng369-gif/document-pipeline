using CloudDocumentPipeline.Infrastructure;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.NotificationService;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using CloudDocumentPipeline.Application.Abstractions.Observability;

// NotificationService is an independent event consumer for secondary job side effects.
var loggerConfiguration = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/notification-.log",
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

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("CloudDocumentPipeline.NotificationService"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(CloudDocumentPipelineTracing.SourceName)
            .AddHttpClientInstrumentation();

        if (!IsCloudEnvironment())
        {
            tracing.AddConsoleExporter();
        }
    });

// Reuse the same infrastructure bindings as API and worker, then add notification-specific services.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<NotificationEmailSender>();

var messagingSettings = builder.Configuration
    .GetSection(MessagingSettings.SectionName)
    .Get<MessagingSettings>() ?? new MessagingSettings();

if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<ServiceBusNotificationWorker>();
}
else
{
    builder.Services.AddHostedService<NotificationWorker>();
}

builder.Services.AddSerilog();

var host = builder.Build();
host.Run();

static bool IsCloudEnvironment()
{
    var environmentName =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
        string.Empty;

    return environmentName.Equals("Testbed", StringComparison.OrdinalIgnoreCase) ||
           environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);
}
