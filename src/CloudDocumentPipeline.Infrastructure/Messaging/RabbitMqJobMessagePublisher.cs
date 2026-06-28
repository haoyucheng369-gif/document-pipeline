using System.Text;
using CloudDocumentPipeline.Application.Abstractions.Messaging;
using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// RabbitMQ 鍙戝竷鍣細
// 璐熻矗鎶?Outbox 閲岀殑闆嗘垚娑堟伅鐪熸鍙戝竷鍒?topic exchange銆?
// 褰撳墠鐗堟湰鏀规垚澶嶇敤杩涚▼鍐呴暱杩炴帴锛屽彧涓烘瘡娆″彂甯冨垱寤虹煭鐢熷懡鍛ㄦ湡 channel銆?
public sealed class RabbitMqJobMessagePublisher : IJobMessagePublisher
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IRabbitMqTopologyInitializer _topologyInitializer;
    private readonly RabbitMqSettings _settings;

    public RabbitMqJobMessagePublisher(
        IRabbitMqConnectionProvider connectionProvider,
        IRabbitMqTopologyInitializer topologyInitializer,
        RabbitMqSettings settings)
    {
        _connectionProvider = connectionProvider;
        _topologyInitializer = topologyInitializer;
        _settings = settings;
    }

    public Task PublishJobCreatedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // 褰撳墠椤圭洰缁熶竴閫氳繃 PublishRawAsync 鍙戝竷闆嗘垚娑堟伅锛岃繖涓棫鎺ュ彛淇濈暀浣嗕笉鍐嶄娇鐢ㄣ€?
        throw new NotSupportedException("Use PublishRawAsync for integration messages.");
    }

    public Task PublishRawAsync(string messageType, string payloadJson, CancellationToken cancellationToken = default)
    {
        // 姣忔鍙戝竷鍙垱寤轰竴涓煭鐢熷懡鍛ㄦ湡 channel锛?
        // 闀胯繛鎺ユ湰韬敱杩炴帴鎻愪緵鍣ㄧ粺涓€澶嶇敤銆?
        using var channel = _connectionProvider.GetConnection().CreateModel();

        // 鍙戝竷鍓嶇‘淇濇嫇鎵戝瓨鍦紝閬垮厤鏌愪釜鏈嶅姟鍏堝惎鍔ㄦ椂闃熷垪杩樻病琚０鏄庛€?
        _topologyInitializer.EnsureTopology(channel);

        var body = Encoding.UTF8.GetBytes(payloadJson);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        channel.BasicPublish(
            exchange: _settings.TopicExchangeName,
            routingKey: ResolveRoutingKey(messageType),
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    private string ResolveRoutingKey(string messageType)
    {
        // 杩欓噷璐熻矗鎶娾€滄秷鎭被鍨嬧€濇槧灏勬垚鈥淩abbitMQ routing key鈥濄€?
        return messageType switch
        {
            nameof(Application.Messaging.JobCreatedIntegrationMessage) => _settings.JobCreatedRoutingKey,
            nameof(Application.Messaging.JobStatusChangedIntegrationMessage) => _settings.JobStatusChangedRoutingKey,
            _ => throw new NotSupportedException($"Unsupported integration message type '{messageType}'.")
        };
    }
}
