using Banking.Application.Abstractions;
using Banking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace Banking.Api.IntegrationTests.Persistence;

// UnitOfWork'ün provider hatalarını hangi application-level exception'a çevirdiği.
// Veritabanı gerektirmez: çeviri saf bir fonksiyondur, hatalar doğrudan kurulur.
public class UnitOfWorkTranslationTests
{
    [Fact]
    public void Translate_StaleConcurrencyToken_BecomesConcurrencyConflict()
    {
        var translated = UnitOfWork.Translate(new DbUpdateConcurrencyException("stale"));

        translated.ShouldBeOfType<ConcurrencyConflictException>();
    }

    [Fact]
    public void Translate_UniqueViolation_BecomesUniqueConstraintViolationWithConstraintName()
    {
        var translated = UnitOfWork.Translate(
            Postgres(PostgresErrorCodes.UniqueViolation, constraintName: "pk_idempotency_keys"));

        translated.ShouldBeOfType<UniqueConstraintViolationException>()
            .ConstraintName.ShouldBe("pk_idempotency_keys");
    }

    // Aynı hesaplara ters sırada dokunan iki transfer (A→B ile B→A) Postgres'te kilit
    // döngüsü yaratır; Postgres birini kurban seçer. Bu geçici bir çakışmadır: çeviri
    // olmadan istek 500 dönerdi, çevrildiği için mevcut yeniden deneme mekanizması devreye girer.
    [Theory]
    [InlineData(PostgresErrorCodes.DeadlockDetected)]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    public void Translate_TransientSerializationFailure_BecomesConcurrencyConflict(string sqlState)
    {
        var translated = UnitOfWork.Translate(Postgres(sqlState));

        translated.ShouldBeOfType<ConcurrencyConflictException>();
    }

    [Fact]
    public void Translate_UnrecognizedError_IsReturnedUnchanged()
    {
        // Gerçek bir hata (ör. foreign key ihlali) çakışma gibi yeniden denenmemeli,
        // olduğu gibi yukarı çıkıp 500 olarak görünmeli.
        var original = Postgres(PostgresErrorCodes.ForeignKeyViolation);

        UnitOfWork.Translate(original).ShouldBeSameAs(original);
    }

    private static DbUpdateException Postgres(string sqlState, string? constraintName = null) =>
        new(
            "update failed",
            new PostgresException(
                messageText: "postgres error",
                severity: "ERROR",
                invariantSeverity: "ERROR",
                sqlState: sqlState,
                constraintName: constraintName));
}
