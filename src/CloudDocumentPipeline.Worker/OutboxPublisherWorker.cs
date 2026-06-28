using CloudDocumentPipeline.Application.Abstractions.Messaging;
using CloudDocumentPipeline.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CloudDocumentPipeline.Worker;

// 发送侧后台 worker：
// 轮询扫描数据库里的 OutboxMessages，把未发布消息发送到 RabbitMQ。
public sealed class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox publisher worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 每轮扫描都创建一个新的 DI scope，避免长生命周期持有 DbContext。
                using var scope = _scopeFactory.CreateScope();

                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
                var publisher = scope.ServiceProvider.GetRequiredService<IJobMessagePublisher>();

                // 一次取有限批量，避免一次性把所有未发布消息全部读进内存。
                var messages = await outboxRepository.GetUnprocessedAsync(20, stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        // 这里只做“发送消息”这一件事；发送成功后再标记已处理。
                        await publisher.PublishRawAsync(message.Type, message.PayloadJson, stoppingToken);
                        message.MarkProcessed();
                    }
                    catch (Exception ex)
                    {
                        // 发送失败不丢消息，而是留下失败痕迹，下轮继续扫到它。
                        _logger.LogError(ex, "Failed to publish outbox message {OutboxMessageId}", message.Id);
                        message.MarkFailed(ex.Message);
                    }
                }

                // 本轮所有 Outbox 状态变更统一保存。
                await outboxRepository.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // 单轮扫描失败不退出整个 worker，下一轮仍然继续跑。
                _logger.LogError(ex, "Unexpected error in outbox publisher worker.");
            }

            // Outbox 没有 RabbitMQ 那样的推送机制，所以这里用定时轮询数据库。
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
