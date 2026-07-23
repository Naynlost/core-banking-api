# ADR 0007: Hesap versiyonu ile optimistic locking

**Durum:** Kabul edildi (Aşama 4)

## Bağlam

İki transfer aynı hesaptan aynı anda çekerse, ikisi de "bakiye yeterli" görüp ikisi de
işlem yazabilir ve hesap eksiye düşer (lost update / TOCTOU). Defter append-only
olduğundan satır düzeyinde doğal bir yazma çakışması **oluşmaz**: iki INSERT birbirini
engellemez; çakışmayı bizim üretmemiz gerekir. Aynı koruma günlük limit için de
şarttır: limit kontrolü de "oku, karar ver, yaz" örüntüsüdür.

## Karar

- `Account.Version` sayacı eklendi: hesabı ilgilendiren her para hareketi versiyonu
  bir artırır. Kolon EF Core'da **`IsConcurrencyToken`** olarak işaretlidir.
- Böylece iki eşzamanlı işlem aynı hesabın aynı versiyonunu okumuşsa yalnızca biri
  commit edebilir; diğeri `DbUpdateConcurrencyException` alır (UnitOfWork bunu
  application-level bir çakışma hatasına çevirir).
- Kaybeden taraf otomatik **yeniden dener**: taze bir DI scope'uyla en fazla 3 deneme;
  güncel bakiye/limitle kurallar yeniden değerlendirilir. Denemeler tükenirse
  `transfer.conflict` → HTTP 409 döner ve istemci tekrar edebilir (idempotency
  anahtarı sayesinde güvenle).

## Değerlendirilen alternatif

**Pessimistic locking** (`SELECT ... FOR UPDATE`): doğruluk açısından eşdeğer, ama
kilit tutma süresi boyunca eşzamanlılığı düşürür ve kilitleme sırası yönetilmezse
deadlock riski taşır. Çakışmanın nadir olduğu bu iş yükünde optimistic + retry daha
iyi ölçeklenir.

## Sonuçlar

- Paralel yük altında bakiye tutarlıdır; integration testi 20 paralel transferi
  (bakiyenin 2 katı talep) gerçek Postgres'te koşar: overdraft yok, para korunur.
- Bedeli çakışmada yeniden çalıştırılan handler maliyeti ve koddaki retry iskeletidir
  (`IdempotentMovement` ortak iskelete çıkarılmıştır).
