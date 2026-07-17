using Banking.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.UserId).HasMaxLength(64);
        builder.Property(t => t.TokenHash).HasMaxLength(64); // hex SHA-256

        // The rotation lookup: find the presented token by hash.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Reuse detection revokes all of a user's active tokens.
        builder.HasIndex(t => t.UserId);
    }
}
