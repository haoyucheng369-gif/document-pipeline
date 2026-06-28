using CloudDocumentPipeline.Infrastructure;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.NotificationService;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using CloudDocumentPipeline.Application.Abstractions.Observability;

// NotificationService 鍏ュ彛锛?
// 瀹冩槸鐙珛娑堣垂鑰咃紝鍙礋璐ｈ闃呴€氱煡绫讳簨浠跺苟鎵ц閫氱煡鍓綔鐢ㄣ€?
// 褰撳墠寮€濮嬫敮鎸佹寜 Messaging.Provider 鍦?RabbitMQ 鍜?Service Bus 涔嬮棿鍒囨崲銆?
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

// 澶嶇敤鍚屼竴濂?Infrastructure锛屽啀琛ュ厖閫氱煡鍙戦€佸櫒銆?
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
