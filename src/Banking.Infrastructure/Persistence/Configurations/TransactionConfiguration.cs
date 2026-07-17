using Banking.Domain.Ledgers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(ValueConverters.TransactionId)
            .ValueGeneratedNever();

        builder.Property(t => t.Description)
            .HasMaxLength(200);

        // Get-only properties are not discovered by convention; map explicitly.
        builder.Property(t => t.OccurredAt);

        builder.Property(t => t.ReversesTransactionId)
            .HasConversion(ValueConverters.TransactionId);

        // At most one reversal per transaction — two racing reversals cannot both land.
        builder.HasIndex(t => t.ReversesTransactionId)
            .IsUnique();

        // Entries live and die with their transaction; deleting either is
        // forbidden anyway (append-only ledger), so restrict cascades.
        builder.HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(t => t.Entries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
