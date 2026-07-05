using CloudDocumentPipeline.Api.Extensions;
using CloudDocumentPipeline.Api.Observability;
using CloudDocumentPipeline.Api.Realtime;
using CloudDocumentPipeline.Api.Validators;
using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Jobs;
using CloudDocumentPipeline.Infrastructure;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

const string FrontendCorsPolicy = "FrontendCorsPolicy";

// API host entry point. This process wires HTTP endpoints, SignalR, logging,
// validation, health checks, persistence, storage, and realtime message consumers.
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
    .ConfigureResource(resource => resource.AddService("CloudDocumentPipeline.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(CloudDocumentPipelineTracing.SourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!IsCloudEnvironment())
        {
            tracing.AddConsoleExporter();
        }
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
{
    // Keep the SignalR connection tolerant of local debugging pauses.
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
});
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>();
builder.Services.AddScoped<ICorrelationContextAccessor, HttpCorrelationContextAccessor>();

// Local and testbed clients may run from different origins. Tighten this to an
// explicit allowlist before exposing a production API publicly.
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

builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: ["live"])
    .AddDbContextCheck<AppDbContext>(tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infrastructure selects SQL, storage, and messaging implementations from configuration.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();

var enableJobStatusConsumer =
    builder.Configuration.GetValue("Realtime:EnableJobStatusConsumer", true);

var messagingSettings = builder.Configuration
    .GetSection(MessagingSettings.SectionName)
    .Get<MessagingSettings>() ?? new MessagingSettings();

if (enableJobStatusConsumer)
{
    // Realtime consumers translate job status events from the broker into SignalR updates.
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

// Correlation and exception handling run first so later middleware shares one request context.
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
// Browsers subscribe here for job status changes.
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
