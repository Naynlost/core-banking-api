using Banking.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(128);
        builder.Property(m => m.Payload).HasColumnType("jsonb");
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Property(m => m.TraceParent).HasMaxLength(64);
        builder.Property(m => m.CorrelationId).HasMaxLength(64);

        // Publisher'ın polling sorgusu: işlenmemiş satırlar geliş sırasında
        builder.HasIndex(m => m.OccurredAt).HasFilter("processed_at IS NULL");
    }
}

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(m => new { m.Consumer, m.MessageId });
        builder.Property(m => m.Consumer).HasMaxLength(128);
    }
}
