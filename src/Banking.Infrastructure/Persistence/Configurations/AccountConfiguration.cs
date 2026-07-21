using Banking.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(ValueConverters.AccountId)
            .ValueGeneratedNever();

        builder.Property(a => a.Owner)
            .HasMaxLength(200);

        builder.Property(a => a.Currency)
            .HasConversion(ValueConverters.Currency)
            .HasMaxLength(3);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.KycStatus)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.DailyTransferLimit)
            .HasPrecision(18, 2);

        // Postgres'te rowversion tipi yok, bu yüzden uygulama yönetimli concurrency token kullanılır
        builder.Property(a => a.Version)
            .IsConcurrencyToken();
    }
}
