# ADR 0010 — Çapraz kur transferi: para birimi başına denge ve FX pozisyon hesapları

**Durum:** Kabul edildi (Aşama 11)

## Bağlam

Hesaplar tek para biriminde açılıyordu ve farklı para birimindeki iki hesap arasında
transfer reddediliyordu (`ledger.currency_mismatch`). Çapraz kur transferi eklemek
istendiğinde defterin temel kuralıyla doğrudan çakışan bir sorun çıktı:

`Transaction` iki değişmez uyguluyordu — tüm satırlar aynı para biriminde olmalı ve
toplam borç toplam alacağa eşit olmalı. "1.000 TRY borç + 24 USD alacak" biçiminde bir
işlem bu kurala göre dengesizdir ve **haklı olarak** öyledir: farklı para birimleri
toplanamaz, 1.000 ile 24'ü karşılaştırmak anlamsızdır. Yani çapraz kur işlemi, tek bir
toplam üzerinden dengelenemez.

Değerlendirilen seçenekler:

1. **Çevrimi kayıt altına alıp tek bacak yazmak** (ör. hedef hesaba doğrudan 24 USD
   alacak, kaynaktan 1.000 TRY borç ve "kur şuydu" diye bir not). Bu, çift taraflı
   defteri bozar: her para biriminde sistemin toplamı artık sıfır olmaz, para bir
   birimde yoktan var olur, diğerinde buharlaşır. Projenin bütün varlık nedeni bu
   invariant olduğu için elendi.
2. **Her para birimi için ayrı defter tutmak.** Denge korunur ama iki defteri birbirine
   bağlayan hiçbir kayıt kalmaz; bir transferin iki yarısı ilişkisizleşir ve mutabakat
   imkânsızlaşır.
3. **Para birimi başına denge + FX pozisyon hesapları** (seçilen).

## Karar

`Transaction`'ın değişmezi "global denge"den **"her para biriminde ayrı ayrı denge"**ye
çevrildi. Tek para birimli işlem bunun özel hâlidir, dolayısıyla mevcut tüm işlemler
aynen geçerli kalır ve `transaction.mixed_currencies` hatası ortadan kalkar.

Çapraz kur transferi dört satır üretir ve arada bankanın **FX pozisyon hesapları** durur:

```
TRY bacağı:  müşteri A            borç   1.000 TRY
             banka TRY pozisyonu  alacak 1.000 TRY   → TRY dengeli
USD bacağı:  banka USD pozisyonu  borç      24 USD
             müşteri B            alacak    24 USD   → USD dengeli
```

Pozisyon hesapları `AccountType.FxPosition` ile temsil edilir ve bakiye yönü liability
ile aynıdır (alacak artırır). Bu, hedef para birimindeki pozisyonun **önceden stok
yüklenmiş olmasını zorunlu kılar**: banka elinde olmayan dövizi satamaz. Stok yetmezse
transfer `ledger.insufficient_fx_liquidity` ile reddedilir. Stok yükleme, hazine rolüne
(`treasury`) açık ayrı bir uçtur ve kendisi de dengeli bir işlemdir (kasa borçlanır,
pozisyon alacaklanır).

Kur kaynağı `IExchangeRateProvider` arayüzünün arkasındadır; mevcut implementasyon
kurları yapılandırmadan okur. Çevrim ve yuvarlama matematiği domain'deki `ExchangeRate`
value object'inde toplanır: sonuç defterin ölçeğine (2 hane) **bankacılık yuvarlamasıyla**
indirilir ve sıfıra yuvarlanan tutar `fx.amount_too_small` ile reddedilir.

## Sonuçlar

- Defterin temel garantisi korunur: artık **her para biriminde ayrı ayrı** para yoktan
  var olamaz, vara yok olamaz. Bu, dört satırın tek bir transaction'da atomik yazılmasıyla
  birlikte, transferin iki yarısının asla ayrışamayacağı anlamına gelir.
- Bankanın döviz pozisyonu ve dolayısıyla kur riski defterde görünür hâle gelir; pozisyon
  bakiyesi "banka bu para biriminde ne kadar açık/fazla taşıyor" sorusunun cevabıdır.
- Yuvarlama farkı bankanın pozisyonunda kalır. Bu doğrudur ama küçük bir kâr/zarar
  kalemidir; ayrı bir yuvarlama farkı hesabında izlenmesi ileriye bırakılmıştır.
- Yeni bir başarısızlık senaryosu doğar: likidite yetersizliği. Bu, gerçek hayattaki
  durumun karşılığıdır ancak demo akışında önce stok yüklemeyi gerektirir.
- Kurlar yapılandırmadan geldiği için testler ve CI ağa bağımlı değildir; gerçek bir
  kurulumda arayüzün arkasına kur beslemesi takmak yeterlidir. Şu hâliyle **spread/komisyon
  yoktur** (tek mid-rate) ve kurun zaman içindeki değişimi saklanmaz.
- `AccountType` string olarak saklandığı için yeni değer şema değişikliği gerektirmedi.
