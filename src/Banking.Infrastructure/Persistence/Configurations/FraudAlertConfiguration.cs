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

        // At-least-once delivery may screen the same transfer twice; one alert
        // per (transaction, rule) keeps redeliveries from piling up duplicates.
        builder.HasIndex(a => new { a.TransactionId, a.Rule })
            .IsUnique();

        // Reviewers work off the open queue; the status filter is indexed for it.
        builder.HasIndex(a => a.Status);
    }
}
