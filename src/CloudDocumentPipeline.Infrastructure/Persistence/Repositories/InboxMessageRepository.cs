using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Domain.Inbox;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CloudDocumentPipeline.Infrastructure.Persistence.Repositories;

public sealed class InboxMessageRepository : IInboxMessageRepository
{
    private readonly AppDbContext _dbContext;

    public InboxMessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // 杩欓噷鐨?claim 涓嶆槸鈥滃０鏄庘€濓紝鑰屾槸鈥滄姠鍗犺繖鏉℃秷鎭殑澶勭悊鏉冣€濄€?
    // 渚濊禆 (MessageId, ConsumerName) 鍞竴閿紝鍙湁涓€涓疄渚嬭兘棣栨鎻掑叆鎴愬姛銆?
    // 濡傛灉鏃ц褰曞凡缁?Failed锛屾垨 Processing 瓒呮椂浜嗭紝鍒欏厑璁稿綋鍓嶅疄渚嬮噸鏂版帴绠°€?
    public async Task<bool> TryClaimAsync(
        Guid messageId,
        string consumerName,
        TimeSpan processingTimeout,
        CancellationToken cancellationToken = default)
    {
        // 榛樿鍏堝皾璇曟彃鍏ヤ竴鏉℃柊鐨?Inbox 璁板綍锛屽垵濮嬬姸鎬佹槸 Processing銆?
        var inboxMessage = new InboxMessage(messageId, consumerName);
        await _dbContext.InboxMessages.AddAsync(inboxMessage, cancellationToken);

        try
        {
            // 鎻掑叆鎴愬姛锛岃鏄庡綋鍓嶅疄渚嬫姠鍒颁簡澶勭悊鏉冦€?
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // 鍞竴閿啿绐佽〃绀哄悓涓€涓?consumer 宸茬粡瑙佽繃杩欐潯娑堟伅浜嗐€?
            // 杩欓噷涓嶇洿鎺ュけ璐ワ紝鑰屾槸缁х画鍒ゆ柇锛氭槸宸插鐞嗐€佸鐞嗕腑锛岃繕鏄彲浠ラ噸鏂版帴绠°€?
            _dbContext.Entry(inboxMessage).State = EntityState.Detached;

            var existingInbox = await GetByMessageIdAsync(messageId, consumerName, cancellationToken);
            if (existingInbox is null)
            {
                return false;
            }

            if (existingInbox.Status == InboxStatus.Processed)
            {
                // 宸叉垚鍔熷鐞嗚繃鐨勬秷鎭紝涓嶅啀閲嶅鎵ц銆?
                return false;
            }

            if (existingInbox.Status == InboxStatus.Failed ||
                existingInbox.IsStale(DateTime.UtcNow, processingTimeout))
            {
                // 澶辫触娑堟伅锛屾垨澶勭悊瓒呮椂鍗′綇鐨勬秷鎭紝鍏佽閲嶆柊 claim 鎺ョ銆?
                existingInbox.Reclaim();
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }

            // 鍏朵綑鎯呭喌閫氬父鏄€滃埆浜烘鍦ㄦ甯稿鐞嗏€濓紝褰撳墠瀹炰緥鏀惧純銆?
            return false;
        }
    }

    // 璇诲彇宸?claim 鐨?Inbox 璁板綍锛屽悗缁湪浜嬪姟閲屾妸瀹冩洿鏂版垚 Processed / Failed銆?
    public async Task<InboxMessage?> GetByMessageIdAsync(
        Guid messageId,
        string consumerName,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InboxMessages
            .FirstOrDefaultAsync(
                x => x.MessageId == messageId && x.ConsumerName == consumerName,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        // 涓嶅悓鏁版嵁搴撴彁渚涚▼搴忕殑鍞竴閿紓甯哥爜涓嶅悓锛岃繖閲岀粺涓€灏佽鎴愪竴涓垽鏂€?
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        if (exception.InnerException is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode == 19;
        }

        return false;
    }
}
