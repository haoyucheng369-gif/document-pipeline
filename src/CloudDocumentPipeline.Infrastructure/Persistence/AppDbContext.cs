using CloudDocumentPipeline.Domain.Inbox;
using CloudDocumentPipeline.Domain.Jobs;
using CloudDocumentPipeline.Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CloudDocumentPipeline.Infrastructure.Persistence;

// EF Core persistence boundary for jobs plus the outbox/inbox reliability tables.
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
        // Jobs keep business state; file bytes live in storage providers.
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

        // Outbox rows are committed with business changes and later published by a worker.
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Type).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
        });

        // Inbox rows make message consumption idempotent per consumer.
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ConsumerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            // The unique index is the database-level claim guard for concurrent consumers.
            entity.HasIndex(x => new { x.MessageId, x.ConsumerName }).IsUnique();
        });
    }
}
