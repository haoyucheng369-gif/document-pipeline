namespace CloudDocumentPipeline.Infrastructure.Messaging;

// Azure Service Bus 閰嶇疆锛?
// 褰撳墠鍏堟寜 Topic + Subscription 妯″瀷璁捐锛屽垎鍒粰 worker / notification / api-realtime 浣跨敤銆?
// 杩欐牱鑳芥瘮杈冩帴杩戝師鏉?RabbitMQ 涓€鏉℃秷鎭澶氫釜娑堣垂鑰呭垎鍒鐞嗙殑璇箟銆?
public sealed class ServiceBusSettings
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public string TopicName { get; set; } = "job-events";
    public string WorkerSubscriptionName { get; set; } = "worker";
    public string NotificationSubscriptionName { get; set; } = "notification";
    public string ApiRealtimeSubscriptionName { get; set; } = "api-realtime";
}
