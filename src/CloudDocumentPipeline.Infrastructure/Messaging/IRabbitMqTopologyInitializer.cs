using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// RabbitMQ 鎷撴墤鍒濆鍖栧櫒鎺ュ彛锛?
// 缁熶竴澹版槑 exchange / queue / binding锛岄伩鍏嶅悇澶勯噸澶嶇淮鎶や竴濂?RabbitMQ 缁撴瀯銆?
public interface IRabbitMqTopologyInitializer
{
    // 瀵瑰綋鍓?channel 琛ラ綈褰撳墠绯荤粺鎵€闇€鐨勬墍鏈夋嫇鎵戠粨鏋勩€?
    void EnsureTopology(IModel channel);
}
