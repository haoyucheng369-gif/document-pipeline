using CloudDocumentPipeline.Domain.Inbox;

namespace CloudDocumentPipeline.Application.Abstractions.Persistence;

public interface IInboxMessageRepository
{
    // 灏濊瘯鍏堝啓鍏ヤ竴鏉?Processing 璁板綍锛屼緷闈犲敮涓€閿姠鍗犺繖鏉℃秷鎭殑澶勭悊鏉冦€?
    // 濡傛灉鍘嗗彶璁板綍鏄?Failed锛屾垨鑰?Processing 宸茶秴鏃讹紝鍒欏厑璁搁噸鏂?claim銆?
    // true 琛ㄧず褰撳墠瀹炰緥鎷垮埌浜嗗鐞嗘潈锛沠alse 琛ㄧず鍒汉杩樺湪澶勭悊鎴栧凡缁忓鐞嗗畬鎴愩€?
    Task<bool> TryClaimAsync(Guid messageId, string consumerName, TimeSpan processingTimeout, CancellationToken cancellationToken = default);

    // 鏍规嵁娑堟伅鏍囪瘑鍜屾秷璐硅€呭悕绉板彇鍥?Inbox 璁板綍锛?
    // 鍚庣画浼氬湪浜嬪姟閲屾妸瀹冩洿鏂版垚 Processed 鎴?Failed銆?
    Task<InboxMessage?> GetByMessageIdAsync(Guid messageId, string consumerName, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
