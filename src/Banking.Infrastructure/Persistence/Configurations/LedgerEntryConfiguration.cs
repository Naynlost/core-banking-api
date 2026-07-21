using Banking.Domain.Accounts;
using Banking.Domain.Ledgers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.TransactionId)
            .HasConversion(ValueConverters.TransactionId);

        builder.Property(e => e.AccountId)
            .HasConversion(ValueConverters.AccountId);

        builder.Property(e => e.Direction)
            .HasConversion<string>()
            .HasMaxLength(8);

        // Get-only property, convention ile keşfedilmez; elle map edilir
        builder.Property(e => e.OccurredAt);

        // Money iki koloma açılır; tek kolonluk converter tutarın veya para biriminin kaybolmasına yol açardı
        builder.ComplexProperty(e => e.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasPrecision(18, 2);

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasConversion(ValueConverters.Currency)
                .HasMaxLength(3);
        });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
