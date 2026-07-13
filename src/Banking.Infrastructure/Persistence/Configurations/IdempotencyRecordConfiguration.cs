using Banking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_keys");

        // Composite key: the same idempotency key from different users must not collide.
        builder.HasKey(r => new { r.Key, r.UserId });

        builder.Property(r => r.Key).HasMaxLength(128);
        builder.Property(r => r.UserId).HasMaxLength(64);
        builder.Property(r => r.TransactionId);
        builder.Property(r => r.CreatedAt);
    }
}
