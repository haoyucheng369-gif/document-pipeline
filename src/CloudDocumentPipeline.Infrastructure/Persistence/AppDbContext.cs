using CloudDocumentPipeline.Domain.Inbox;
using CloudDocumentPipeline.Domain.Jobs;
using CloudDocumentPipeline.Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CloudDocumentPipeline.Infrastructure.Persistence;

// EF Core DbContext：
// 负责把领域对象映射成数据库表结构。
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Jobs 是业务主表，记录任务本身的状态和结果。
        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("Jobs");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        });

        // OutboxMessages 是发送侧可靠消息表，负责记录“待发布到 MQ 的消息”。
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Type).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
        });

        // InboxMessages 是消费侧去重/claim/恢复表，按 (MessageId, ConsumerName) 唯一。
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ConsumerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            // 同一个 consumer 对同一条消息，只能有一条 Inbox 记录。
            entity.HasIndex(x => new { x.MessageId, x.ConsumerName }).IsUnique();
        });
    }
}
