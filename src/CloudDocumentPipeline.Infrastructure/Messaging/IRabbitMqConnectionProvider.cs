using RabbitMQ.Client;

namespace CloudDocumentPipeline.Infrastructure.Messaging;

// RabbitMQ 杩炴帴鎻愪緵鍣ㄦ帴鍙ｏ細
// 缁熶竴鍚戝彂甯冨櫒鍜屾秷璐硅€呮彁渚涘彲澶嶇敤鐨勯暱鏈熻繛鎺ャ€?
public interface IRabbitMqConnectionProvider : IDisposable
{
    // 鑾峰彇褰撳墠杩涚▼鍐呭彲澶嶇敤鐨?RabbitMQ 杩炴帴銆?
    IConnection GetConnection();
}
