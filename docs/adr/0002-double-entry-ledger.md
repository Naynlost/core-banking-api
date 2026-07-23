# ADR 0002: Tek bakiye kolonu yerine çift taraflı defter

**Durum:** Kabul edildi (Aşama 1)

## Bağlam

En basit tasarım, hesapta bir `balance` kolonu tutup her işlemde artırıp azaltmaktır.
Bu tasarımın bilinen zayıflıkları vardır:

- Bir güncelleme kaybolur veya iki kez uygulanırsa **bunu fark etmenin yolu yoktur**;
  bakiye "neyse odur", tarihçesi kanıt üretmez.
- Para bir hesaptan çıkıp diğerine girmeden süreç kesilirse para "buharlaşabilir"
  veya "türeyebilir"; sistemin toplamı üzerinde bir değişmez (invariant) yoktur.
- Denetim (audit) ve mutabakat (reconciliation) için ayrı bir log tutmak gerekir ve o
  log ile bakiye birbirinden sapabilir.

Gerçek finansal sistemler bu yüzden yüzyıllardır çift taraflı muhasebe kullanır.

## Karar

Bakiye hiçbir yerde saklanmaz; **defterden türetilir**:

- Her finansal hareket bir `Transaction` ve en az iki `LedgerEntry` üretir: bir hesap
  borçlanır (negatif), diğeri alacaklanır (pozitif) ve **satırların toplamı her zaman
  sıfırdır**. Bu kural domain'de (`Ledger`) zorlanır.
- Defter **append-only**'dir: satır silinmez, güncellenmez. Düzeltme gerekiyorsa ters
  kayıt (reversal) atılır (ADR kapsamı: `ReversalPolicy`).
- Yatırma/çekme bile çift taraflıdır: karşı bacak bankanın kasa (cash) hesabına yazılır.

## Sonuçlar

- Para yoktan var olamaz, vara yok olamaz: sistemin toplamı yapısal olarak sıfırdır ve
  bu, testlerle değil veri modeliyle garanti edilir.
- Her bakiye her an yeniden hesaplanabilir ve tarihçe kendi kendinin denetim kaydıdır.
- Bedeli okuma maliyetidir: bakiye okumak `SUM` gerektirir ve defter büyüdükçe pahalanır.
  Kabul edilen çözüm, kaynağı defter olan bir okuma projeksiyonu eklemektir (bkz.
  bakiye projeksiyonu); projeksiyon her zaman defterden yeniden inşa edilebilir.
- Şema, alışıldık CRUD modelinden daha az sezgiseldir; bu bedel, README ve bu ADR ile
  belgelenerek ödenir.
