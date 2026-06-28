using System.Text;
using Azure.Messaging.ServiceBus;
using CloudDocumentPipeline.Application.Abstractions.Messaging;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Service Bus 鍙戦€佷晶瀹炵幇锛?
// 鍙礋璐ｆ妸 Outbox 閲岀殑闆嗘垚娑堟伅鍙戝埌 Azure Service Bus Topic銆?
// 褰撳墠鍏堜繚鐣欏師鏈?IJobMessagePublisher 鎺ュ彛锛岃繖鏍蜂笂灞備笟鍔″拰 OutboxPublisherWorker 涓嶉渶瑕佹敼鍔ㄣ€?
public sealed class ServiceBusJobMessagePublisher : IJobMessagePublisher
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusSettings _settings;

    public ServiceBusJobMessagePublisher(
        ServiceBusClient serviceBusClient,
        ServiceBusSettings settings)
    {
        _serviceBusClient = serviceBusClient;
        _settings = settings;
    }

    public Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use PublishRawAsync for integration messages.");
    }

    public async Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default)
    {
        // 姣忔鍙戦€佸垱寤虹煭鐢熷懡鍛ㄦ湡 sender銆?
        // 鍏堟眰鍒囨崲绠€鍗曟竻鏅帮紝鍚庨潰濡傛灉纭鏈夋槑鏄炬€ц兘鍘嬪姏锛屽啀鑰冭檻鍋?sender 澶嶇敤銆?
        await using var sender = _serviceBusClient.CreateSender(_settings.TopicName);

        // 鎶婃秷鎭被鍨嬪悓鏃舵斁鍒?Subject 鍜屽簲鐢ㄥ睘鎬ч噷锛?
        // Subject 鏂逛究 Service Bus 渚ц瀵燂紝ApplicationProperties 鏂逛究鍚庣画鍏煎璇诲彇銆?
        var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(payloadJson))
        {
            Subject = messageType,
            ContentType = "application/json"
        };

        message.ApplicationProperties["messageType"] = messageType;

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
