namespace CloudDocumentPipeline.Infrastructure.Messaging;

// 娑堟伅鍩虹璁炬柦鎬诲紑鍏筹細
// 鐢ㄦ潵鍐冲畾褰撳墠鐜璧?RabbitMq 杩樻槸 Azure Service Bus銆?
// 杩欐牱鏈湴鍙互缁х画淇濈暀 RabbitMQ 璋冭瘯浣撻獙锛宼estbed/prod 鍐嶅垏鍒颁簯涓婃秷鎭湇鍔°€?
public sealed class MessagingSettings
{
    public const string SectionName = "Messaging";

    public string Provider { get; set; } = "RabbitMq";
}
