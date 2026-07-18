using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class AccountBalanceConfiguration : IEntityTypeConfiguration<AccountBalance>
{
    public void Configure(EntityTypeBuilder<AccountBalance> builder)
    {
        builder.ToTable("account_balances");

        builder.HasKey(b => b.AccountId);

        builder.Property(b => b.AccountId)
            .HasConversion(ValueConverters.AccountId);

        builder.Property(b => b.Debits)
            .HasPrecision(18, 2);

        builder.Property(b => b.Credits)
            .HasPrecision(18, 2);

        builder.Property(b => b.UpdatedAt);
    }
}
