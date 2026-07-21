using Banking.Application.Messaging;

namespace Banking.Application.Fx;

// Bankanın bir para birimindeki döviz stoğunu artırır. Gerçek hazine sürecinin yerine geçen
// demo ucudur (hesap açmadaki /kyc gibi) ve müşteri değil rol korumalıdır.
// Para hareketi ürettiği için idempotency anahtarı zorunludur.
public sealed record FundFxPositionCommand(
    string IdempotencyKey,
    string Requester,
    decimal Amount,
    string CurrencyCode) : ICommand<Guid>;

// Çevrimi uygulamadan kuru ve hesaplanacak tutarı gösterir; transfer öncesi "ne kadar gider"
// sorusunu cevaplar. Salt okunur, deftere dokunmaz.
public sealed record GetFxQuoteQuery(
    string From,
    string To,
    decimal Amount) : IQuery<FxQuoteResponse>;

public sealed record FxQuoteResponse(
    string From,
    string To,
    decimal Rate,
    decimal Amount,
    decimal ConvertedAmount);

public static class FxTreasury
{
    // Döviz pozisyonunu besleyebilen rol; müşteri token'ı bu uçları göremez
    public const string OperatorRole = "treasury";
}

public static class FxApplicationErrors
{
    public const string Conflict = "fx.conflict";
}
