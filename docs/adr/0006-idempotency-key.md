# ADR 0006 — Para hareketlerinde zorunlu `Idempotency-Key`

**Durum:** Kabul edildi (Aşama 4)

## Bağlam

Ağ güvenilmezdir: istemci yanıtı alamazsa (timeout, bağlantı kopması) isteği tekrar
eder. Para hareketi yapan bir endpoint'te tekrar, **aynı transferin iki kez uygulanması**
demektir. "İstemci tekrar etmesin" bir çözüm değildir; tekrar etmemek de tehlikelidir
(işlem gerçekleşti mi bilinmiyor).

## Karar

- Transfer, yatırma ve çekme endpoint'lerinde **`Idempotency-Key` başlığı zorunludur**;
  anahtar istemci tarafından üretilir (ör. UUID).
- `idempotency_keys` tablosu anahtar kaydını tutar; birincil anahtar **(key, user_id)**
  — bir kullanıcının anahtarı başkasının işlemine çarpamaz.
- Kayıt, para hareketiyle **aynı veritabanı transaction'ında** yazılır: işlem varsa
  anahtar da vardır, işlem rollback olduysa anahtar da yoktur. "Anahtar yazıldı ama
  işlem kayboldu" arası durum yapısal olarak imkânsızdır.
- Aynı anahtar tekrar gelirse yeni işlem yapılmaz; ilk işlemin transaction id'si döner.
- **Eşzamanlı** aynı-anahtar yarışını uygulama kodu değil, veritabanı unique
  constraint'i çözer: kaybeden taraf rollback olur ve kazananın sonucunu döndürür.
- Kayıtlar 24 saat sonra retention job'ı ile silinir; anahtarın ömrü API sözleşmesinin
  parçasıdır.

## Sonuçlar

- İstemci güvenle tekrar edebilir; "timeout yedim, tekrar göndersem çift transfer olur
  mu" sorusu ortadan kalkar. Integration testi bunu gerçek Postgres'te kanıtlar
  (aynı anahtarla iki çağrı → tek transfer).
- Bedeli her para hareketinde bir tablo yazımı ve istemciye anahtar üretme
  yükümlülüğüdür — endüstri standardı bir bedel (Stripe ve benzeri API'ler aynı
  deseni kullanır).
