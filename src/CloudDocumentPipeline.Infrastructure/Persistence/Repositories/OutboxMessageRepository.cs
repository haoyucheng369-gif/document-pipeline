using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CloudDocumentPipeline.Infrastructure.Persistence.Repositories;

// Outbox 仓储：
// 负责持久化和读取待发布消息，不负责真正发送到 RabbitMQ。
public sealed class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly AppDbContext _dbContext;

    public OutboxMessageRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        // 新增一条待发布消息。
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<List<OutboxMessage>> GetUnprocessedAsync(int take, CancellationToken cancellationToken = default)
    {
        // 只拿还没标记 ProcessedAtUtc 的消息，并按创建时间先后发布。
        return await _dbContext.OutboxMessages
            .Where(x => x.ProcessedAtUtc == null)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 统一提交当前 DbContext 中对 Outbox 的状态修改。
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
