namespace Banking.Domain.Accounts;

// Asset hesap (banka kasası) debit ile, liability hesap (müşteri mevduatı) credit ile büyür
public enum AccountType
{
    Asset,
    Liability,

    // Bankanın bir para birimindeki döviz pozisyonu. Çapraz kur transferinde işlemin iki
    // bacağını birbirine bağlar: gönderilen para birimi pozisyonu artar, alınan azalır.
    // Bakiye yönü liability ile aynıdır (alacak artırır).
    FxPosition,
}
