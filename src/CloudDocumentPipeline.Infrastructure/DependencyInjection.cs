using Azure.Messaging.ServiceBus;
using CloudDocumentPipeline.Application.Abstractions.Messaging;
using CloudDocumentPipeline.Application.Abstractions.Observability;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Application.Abstractions.Storage;
using CloudDocumentPipeline.Infrastructure.Messaging;
using CloudDocumentPipeline.Infrastructure.Observability;
using CloudDocumentPipeline.Infrastructure.Persistence;
using CloudDocumentPipeline.Infrastructure.Persistence.Repositories;
using CloudDocumentPipeline.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudDocumentPipeline.Infrastructure;

// Infrastructure 娉ㄥ叆鍏ュ彛锛?
// 褰撳墠鎶娾€滄枃浠跺瓨鍌?provider鈥濆拰鈥滄秷鎭?provider鈥濋兘闆嗕腑鍦ㄨ繖閲屽仛閰嶇疆鍒囨崲銆?
// 杩欐牱搴旂敤灞傚彧渚濊禆鎶借薄鎺ュ彛锛屼笉鐩存帴鍏冲績 Local / AzureBlob / RabbitMq / ServiceBus 鐨勫叿浣撳疄鐜般€?
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        var rabbitMqSettings = configuration
            .GetSection(RabbitMqSettings.SectionName)
            .Get<RabbitMqSettings>() ?? new RabbitMqSettings();

        var messagingSettings = configuration
            .GetSection(MessagingSettings.SectionName)
            .Get<MessagingSettings>() ?? new MessagingSettings();

        var serviceBusSettings = configuration
            .GetSection(ServiceBusSettings.SectionName)
            .Get<ServiceBusSettings>() ?? new ServiceBusSettings();

        var storageSettings = configuration
            .GetSection(StorageSettings.SectionName)
            .Get<StorageSettings>() ?? new StorageSettings();

        services.AddSingleton(rabbitMqSettings);
        services.AddSingleton(messagingSettings);
        services.AddSingleton(serviceBusSettings);
        services.AddSingleton(storageSettings);
        services.AddSingleton<IJobMetrics, JobMetrics>();

        // RabbitMQ 鐩稿叧鍩虹璁炬柦鍏堢户缁敞鍐岋細
        // 鏈湴 Development 浠嶇劧闇€瑕?RabbitMQ 璋冭瘯鑳藉姏锛岀瓑浜戜笂瀹屽叏鍒囧畬鍚庡啀鍐冲畾瑕佷笉瑕佽繘涓€姝ユ媶鍒嗘敞鍐屻€?
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();

        if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(serviceBusSettings.ConnectionString))
            {
                throw new InvalidOperationException("ServiceBus provider requires a non-empty ServiceBus:ConnectionString.");
            }

            services.AddSingleton(_ => new ServiceBusClient(serviceBusSettings.ConnectionString));
        }

        if (string.Equals(storageSettings.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }
        else if (string.Equals(storageSettings.Provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported storage provider '{storageSettings.Provider}'. Supported values: Local, AzureBlob.");
        }

        services.AddScoped<IJobRepository, JobRepository>();

        // 鍙戦€佷晶鍏堟寜 provider 鍒囨崲锛?
        // 鏈湴缁х画鍙?RabbitMQ锛宼estbed/prod 鍒囧埌 Service Bus銆?
        // 杩欐牱鍙互鍏堝畬鎴愨€淥utbox -> 娑堟伅鎬荤嚎鈥濈殑杩佺Щ锛屽啀鍒嗛樁娈靛垏娑堣垂鑰呫€?
        if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IJobMessagePublisher, ServiceBusJobMessagePublisher>();
        }
        else
        {
            services.AddScoped<IJobMessagePublisher, RabbitMqJobMessagePublisher>();
        }

        services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();

        return services;
    }
}
