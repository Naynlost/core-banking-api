using Banking.Domain.ValueObjects;
using Shouldly;

namespace Banking.Domain.Tests.ValueObjects;

public class CurrencyTests
{
    [Theory]
    [InlineData("")]
    [InlineData("TL")]
    [InlineData("TRYY")]
    [InlineData("try")]
    [InlineData("T1Y")]
    public void Create_WithInvalidCode_Fails(string code)
    {
        var result = Currency.Create(code);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CurrencyErrors.InvalidCode);
    }

    [Fact]
    public void Create_WithValidCode_Succeeds()
    {
        var result = Currency.Create("TRY");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("TRY");
    }

    [Fact]
    public void Currencies_WithSameCode_AreEqual()
    {
        Currency.Create("TRY").Value.ShouldBe(Currency.Try);
    }
}
