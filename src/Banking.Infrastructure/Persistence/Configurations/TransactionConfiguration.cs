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

        // Get-only property, convention ile keşfedilmez; elle map edilir
        builder.Property(t => t.OccurredAt);

        builder.Property(t => t.ReversesTransactionId)
            .HasConversion(ValueConverters.TransactionId);

        // İşlem başına en fazla bir ters kayıt, yarışan iki reversal aynı anda kazanamaz
        builder.HasIndex(t => t.ReversesTransactionId)
            .IsUnique();

        // Ledger zaten append-only olduğundan silme yasak, cascade'i de kısıtla
        builder.HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(t => t.Entries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
