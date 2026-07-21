using Banking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_keys");

        // Farklı kullanıcılardan gelen aynı key çakışmasın diye composite key
        builder.HasKey(r => new { r.Key, r.UserId });

        builder.Property(r => r.Key).HasMaxLength(128);
        builder.Property(r => r.UserId).HasMaxLength(64);
        builder.Property(r => r.TransactionId);
        builder.Property(r => r.CreatedAt);
    }
}
