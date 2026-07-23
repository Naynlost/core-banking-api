# ADR 0004: İş kuralı hatalarında exception değil Result pattern

**Durum:** Kabul edildi (Aşama 1)

## Bağlam

"Yetersiz bakiye", "kapalı hesap", "günlük limit aşıldı" gibi durumlar bankacılıkta
**hata değil, beklenen iş sonuçlarıdır**. Bunları exception ile modellemek:

- Kontrol akışını görünmez kılar (hangi handler hangi exception'ı fırlatır, imzadan
  okunamaz),
- Exception maliyeti ve stack trace gürültüsü üretir,
- "Beklenen" ile "gerçekten beklenmeyen" (bug, altyapı çökmesi) durumların aynı
  mekanizmada karışmasına yol açar.

## Karar

- Domain ve Application, iş kuralı sonuçlarını **`Result` / `Result<T>`** ile döndürür.
  Başarısızlık, makine-okunur bir hata koduyla taşınır (ör. `ledger.insufficient_funds`,
  `account.kyc_not_verified`, `ledger.daily_limit_exceeded`).
- API katmanı bu kodları ProblemDetails'e eşler: `*.not_found` → 404,
  `transfer.conflict` → 409, diğer iş kuralı ihlalleri → 400. Eşleme tek yerdedir.
- Exception yalnızca **gerçekten beklenmeyen** durumlara ayrılmıştır (bug, veritabanı
  erişilemez); bunlar `GlobalExceptionHandler` ile 500'e düşer ve loglanır.

## Sonuçlar

- Bir handler'ın üretebileceği sonuçlar tipinden bellidir; testler `IsSuccess`/`Error`
  üzerinden okunaklıdır ve exception yakalamaz.
- Hata kodları API sözleşmesinin parçasıdır: istemci `code` alanına göre davranabilir,
  mesaj metni serbestçe değişebilir.
- Bedeli her katmanda `Result`'ı elle taşımaktır (railway-oriented zincirleme yoktur);
  handler'lar kısa tutulduğu için bu maliyet kabul edilebilir bulundu.
