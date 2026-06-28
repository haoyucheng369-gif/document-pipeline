using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// RabbitMQ 鎷撴墤鍒濆鍖栧櫒瀹炵幇锛?
// 褰撳墠椤圭洰闇€瑕佺殑涓婚槦鍒椼€侀噸璇曢槦鍒椼€丏LQ銆侀€氱煡闃熷垪鍜岀姸鎬佹洿鏂伴槦鍒楅兘鍦ㄨ繖閲岀粺涓€澹版槑銆?
public sealed class RabbitMqTopologyInitializer : IRabbitMqTopologyInitializer
{
    private readonly RabbitMqSettings _settings;

    public RabbitMqTopologyInitializer(RabbitMqSettings settings)
    {
        _settings = settings;
    }

    public void EnsureTopology(IModel channel)
    {
        // 鏍稿績 topic exchange锛氭墍鏈変笟鍔℃秷鎭厛鍙戝埌杩欓噷锛屽啀鎸?routing key 璺敱銆?
        channel.ExchangeDeclare(
            exchange: _settings.TopicExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        // 鏈€缁堝け璐ユ秷鎭繘鍏?DLQ锛屼緵浜哄伐鎺掓煡鎴栬ˉ鍋裤€?
        channel.QueueDeclare(
            queue: _settings.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var retryQueueArguments = new Dictionary<string, object>
        {
            // retry queue 閲岀殑娑堟伅 TTL 鍒版湡鍚庯紝鍐嶉€氳繃 dead-letter 鍥炰富浜ゆ崲鏈恒€?
            ["x-dead-letter-exchange"] = _settings.TopicExchangeName,
            ["x-dead-letter-routing-key"] = _settings.JobCreatedRoutingKey
        };

        channel.QueueDeclare(
            queue: _settings.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryQueueArguments);

        var mainQueueArguments = new Dictionary<string, object>
        {
            // 涓婚槦鍒楅噷鏈€缁堝鐞嗕笉浜嗙殑娑堟伅锛宒ead-letter 鍒?DLQ銆?
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = _settings.DeadLetterQueueName
        };

        channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments);
        channel.QueueBind(_settings.QueueName, _settings.TopicExchangeName, _settings.JobQueueBindingKey);

        // 閫氱煡闃熷垪锛氱敱閫氱煡鏈嶅姟鍗曠嫭娑堣垂锛屽拰涓讳笟鍔″鐞嗚В鑰︺€?
        channel.QueueDeclare(
            queue: _settings.NotificationQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(_settings.NotificationQueueName, _settings.TopicExchangeName, _settings.NotificationQueueBindingKey);

        channel.QueueDeclare(
            queue: _settings.JobStatusUpdatesQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        // 鐘舵€佹洿鏂伴槦鍒楋細API 娑堣垂鍚庯紝鍐嶈浆鎴?SignalR 鎺ㄩ€佺粰鍓嶇銆?
        channel.QueueBind(
            _settings.JobStatusUpdatesQueueName,
            _settings.TopicExchangeName,
            _settings.JobStatusUpdatesBindingKey);
    }
}
