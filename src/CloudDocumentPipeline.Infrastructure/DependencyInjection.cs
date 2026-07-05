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

// Centralizes provider selection for persistence, storage, messaging, and metrics.
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

        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<IRabbitMqTopologyInitializer, RabbitMqTopologyInitializer>();

        // Service Bus is only constructed when the configured broker provider needs it.
        if (string.Equals(messagingSettings.Provider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(serviceBusSettings.ConnectionString))
            {
                throw new InvalidOperationException("ServiceBus provider requires a non-empty ServiceBus:ConnectionString.");
            }

            services.AddSingleton(_ => new ServiceBusClient(serviceBusSettings.ConnectionString));
        }

        // Storage provider switches between local development and Azure runtime models.
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

        // Publishers share the same application abstraction regardless of RabbitMQ or Service Bus.
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
