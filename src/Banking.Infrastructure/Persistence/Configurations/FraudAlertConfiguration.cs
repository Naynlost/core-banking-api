using Banking.Domain.Fraud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class FraudAlertConfiguration : IEntityTypeConfiguration<FraudAlert>
{
    public void Configure(EntityTypeBuilder<FraudAlert> builder)
    {
        builder.ToTable("fraud_alerts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.TransactionId)
            .HasConversion(ValueConverters.TransactionId);

        builder.Property(a => a.Rule)
            .HasMaxLength(64);

        builder.Property(a => a.Detail)
            .HasMaxLength(500);

        builder.Property(a => a.FlaggedAt);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.ResolvedAt);

        builder.Property(a => a.ResolutionNote)
            .HasMaxLength(500);

        // At-least-once teslimat aynı transferi iki kez tarayabilir, unique index tekrar teslimatları engeller
        builder.HasIndex(a => new { a.TransactionId, a.Rule })
            .IsUnique();

        builder.HasIndex(a => a.Status);
    }
}
