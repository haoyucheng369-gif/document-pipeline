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

// API 杩涚▼鍏ュ彛锛?
// 璐熻矗缁勮 HTTP API銆丼ignalR銆佹棩蹇椼€佷腑闂翠欢銆佸熀纭€璁炬柦渚濊禆鍜屽疄鏃舵秷鎭秷璐硅€呫€?
// 褰撳墠鍦ㄥ師鏈夌粨鏋勪笂琛ュ厖浜嗘寜 Messaging.Provider 鍦?RabbitMQ 鍜?Service Bus 涔嬮棿鍒囨崲瀹炴椂娑堣垂鑰呯殑鑳藉姏銆?
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

// 娉ㄥ唽 API 灞傚父瑙勮兘鍔涳細
// 鎺у埗鍣ㄣ€丼ignalR銆丳roblemDetails銆丗luentValidation銆丆orrelationId 璁块棶鍣ㄣ€?
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
{
    // 璋冭瘯 SignalR 鏃讹紝鍚庣濡傛灉鍥犱负鏂偣鏆傚仠杈冧箙锛岄粯璁よ秴鏃朵細姣旇緝瀹规槗鏂嚎銆?
    // 杩欓噷鎶婃湇鍔＄瓒呮椂绐楀彛鏀惧锛屽噺灏戣皟璇曢樁娈电殑鍋囨€ф柇杩炪€?
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
});
builder.Services.AddProblemDetails();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>();
builder.Services.AddScoped<ICorrelationContextAccessor, HttpCorrelationContextAccessor>();

// 褰撳墠椤圭洰涓昏鐢ㄤ簬鏈湴寮€鍙戝拰娴嬭瘯锛屽墠绔潵婧愭殏鏃跺叏閮ㄦ斁寮€銆?
// 鍚庣画涓?testbed / production 鏃讹紝鍙互鏀瑰洖鎸夊煙鍚嶇櫧鍚嶅崟鏀捐銆?
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

// 鍩虹鍋ュ悍妫€鏌ワ細
// - /health/live 鍙垽鏂繘绋嬫湰韬槸鍚﹁繕娲荤潃
// - /health/ready 鍒ゆ柇搴旂敤鏄惁宸茬粡鍑嗗濂芥彁渚涙湇鍔★紝褰撳墠鑷冲皯鍖呮嫭鏁版嵁搴撳彲杩炴帴
builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        tags: ["live"])
    .AddDbContextCheck<AppDbContext>(tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 娉ㄥ唽鍩虹璁炬柦鍜屽簲鐢ㄦ湇鍔°€?
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<JobService>();

// 杩欎釜鍚庡彴娑堣垂鑰呰礋璐ｈ闃呯姸鎬佸彉鍖栦簨浠讹紝
// 鍐嶆妸鐘舵€佸彉鍖栬浆鎴?SignalR 鎺ㄩ€佺粰鍓嶇銆?
// 褰撳墠鍏堜繚鐣欐€诲紑鍏筹紝鍐嶆寜 Messaging.Provider 鍐冲畾鍚姩 RabbitMQ 鐗堣繕鏄?Service Bus 鐗堟秷璐硅€呫€?
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

// 鍏堟寕閾捐矾杩借釜鍜屽叏灞€寮傚父澶勭悊涓棿浠讹紝淇濊瘉鍚庣画鏃ュ織鍜岄敊璇緭鍑虹粺涓€銆?
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
// 鍓嶇閫氳繃杩欎釜 Hub 璁㈤槄 Job 鐘舵€佹洿鏂般€?
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
