using Banking.Domain.StandingOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class StandingOrderConfiguration : IEntityTypeConfiguration<StandingOrder>
{
    public void Configure(EntityTypeBuilder<StandingOrder> builder)
    {
        builder.ToTable("standing_orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        builder.Property(o => o.Owner)
            .HasMaxLength(450);

        builder.Property(o => o.SourceAccountId)
            .HasConversion(ValueConverters.AccountId);

        builder.Property(o => o.DestinationAccountId)
            .HasConversion(ValueConverters.AccountId);

        builder.ComplexProperty(o => o.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2);

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasConversion(ValueConverters.Currency)
                .HasMaxLength(3);
        });

        builder.Property(o => o.Frequency)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Get-only property, convention ile keşfedilmez; elle map edilir
        builder.Property(o => o.NextRunAt);
        builder.Property(o => o.CreatedAt);
        builder.Property(o => o.LastRunAt);

        builder.Property(o => o.LastRunError)
            .HasMaxLength(128);

        // Executor'ın polling sorgusu: aktif emirler vade zamanına göre
        builder.HasIndex(o => new { o.Status, o.NextRunAt });

        builder.HasIndex(o => o.Owner);
    }
}
