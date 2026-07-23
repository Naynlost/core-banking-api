# ADR 0001: Clean Architecture ve içe doğru bağımlılık

**Durum:** Kabul edildi (Aşama 0)

## Bağlam

Bankacılık iş kuralları (defter dengesi, limitler, KYC) projenin en değerli ve en uzun
ömürlü parçasıdır. Framework'ler, ORM'ler ve mesajlaşma altyapısı ise değişebilir.
İş kurallarının EF Core, ASP.NET Core veya RabbitMQ detaylarına sızması hem test
edilebilirliği hem de bu kuralların tek başına okunabilirliğini bozar.

## Karar

Tek solution içinde dört proje, bağımlılık yönü daima içe doğru:

```
Banking.Api → Banking.Application → Banking.Domain
Banking.Infrastructure → Banking.Application, Banking.Domain
```

- **Domain** hiçbir dış pakete bağımlı değildir; entity'ler, value object'ler ve
  iş kuralları saf C#'tır.
- **Application** use case'leri (command/query + handler) ve dış dünyaya açılan
  arayüzleri (`IAccountRepository`, `IUnitOfWork`, `IOutbox`) tanımlar; implementasyon
  bilmez.
- **Infrastructure** bu arayüzleri EF Core, PostgreSQL, RabbitMQ ve Identity ile
  gerçekler.
- **Api** yalnızca HTTP kabuğudur: controller, middleware, DI kompozisyonu.

## Sonuçlar

- Domain kuralları veritabanı olmadan milisaniyeler içinde test edilir (68 domain testi
  hiçbir altyapı gerektirmez).
- ORM veya mesaj kuyruğu değişse Domain ve Application'a dokunulmaz.
- Bedeli dolaylılıktır: her dış bağımlılık için Application'da bir arayüz, Infrastructure'da
  bir implementasyon yazılır. Bu proje boyutunda maliyet düşük, kazanç yüksektir.
- Kural derleyici tarafından tam zorlanamaz (Infrastructure yanlışlıkla Api'yi referans
  alabilir); bu boşluk mimari testlerle (NetArchTest) kapatılır, bkz. test projesindeki
  `ArchitectureTests`.
