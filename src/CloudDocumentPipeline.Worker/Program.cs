using CloudDocumentPipeline.Application.Abstractions.Processing;
using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Infrastructure;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.Worker;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Formatting.Compact;

// Worker 杩涚▼鍏ュ彛锛?
// 褰撳墠寮€濮嬫敮鎸佲€滄湰鍦?RabbitMQ / testbed ServiceBus鈥濆弻 provider銆?
// 鍏堜繚鐣?OutboxPublisherWorker 鍜?StaleInboxRecoveryWorker锛?
// 鍐嶆寜 Messaging.Provider 鍐冲畾鍒板簳鍚姩 RabbitMqWorker 杩樻槸 ServiceBusWorker銆?
QuestPDF.Settings.License = LicenseType.Community;

var loggerConfiguration = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: "logs/worker-.log",
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
    .ConfigureResource(resource => resource.AddService("CloudDocumentPipeline.Worker"))
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

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IJobSideEffectExecutor, JobSideEffectExecutor>();

var messagingSettings = builder.Configuration
    .GetSection(MessagingSettings.SectionName)
    .Get<MessagingSettings>() ?? new MessagingSettings();

builder.Services.AddHostedService<OutboxPublisherWorker>();

// 杩欓噷鏄?worker 娑堣垂绔殑鍒囨崲鐐癸細
// - Development 缁х画 RabbitMqWorker锛屾柟渚挎湰鍦拌皟璇?
// - Testbed / Production 鍒?ServiceBusWorker锛岃蛋浜戜笂娑堟伅鎬荤嚎
if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<ServiceBusWorker>();
}
else
{
    builder.Services.AddHostedService<RabbitMqWorker>();
}

builder.Services.AddHostedService<StaleInboxRecoveryWorker>();
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
