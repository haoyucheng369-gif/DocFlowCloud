using DocFlowCloud.Domain.Inbox;
using DocFlowCloud.Domain.Jobs;
using DocFlowCloud.Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DocFlowCloud.Infrastructure.Persistence;

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

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Type).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ConsumerName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.MessageId, x.ConsumerName }).IsUnique();
        });
    }
}