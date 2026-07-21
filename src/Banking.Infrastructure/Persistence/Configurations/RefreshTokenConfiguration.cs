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

        // Rotation sorgusu: sunulan token'ı hash'e göre bulur
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Reuse tespiti bu index üzerinden kullanıcının tüm aktif token'larını iptal eder
        builder.HasIndex(t => t.UserId);
    }
}
