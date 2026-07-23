# ADR 0009: Yalnızca ücretsiz ve açık kaynak bağımlılıklar

**Durum:** Kabul edildi (Aşama 0, sürekli geçerli)

## Bağlam

.NET ekosisteminde yaygın bazı kütüphaneler yakın dönemde ticari lisansa geçti:
MediatR (v13+), AutoMapper (v15+), MassTransit (v9), FluentAssertions (v8).
"Bireysel kullanım ücretsiz" istisnaları bugün için yeterli görünse de, bir portföy
projesinin herhangi bir lisans belirsizliği taşıması istenmez ve sürüm tavanına
sabitlenmek zamanla bakım yükü üretir.

## Karar

Projeye **yalnızca kalıcı olarak ücretsiz ve OSI onaylı lisanslı** paketler girer.
Her yeni paket eklenmeden önce lisansı kontrol edilir.

Ticarileşen paketler yerine kullanılanlar:

| İhtiyaç | Ticarileşen | Bizim seçim |
|---|---|---|
| Mediator/CQRS | MediatR v13+ | Kendi dispatcher'ımız (ADR 0005) |
| Nesne eşleme | AutoMapper v15+ | Elle mapping (küçük yüzey; harici araca gerek yok) |
| Mesajlaşma soyutlaması | MassTransit v9 | Resmî `RabbitMQ.Client` (MIT) üzerine ince kendi katmanımız |
| Test assertion'ları | FluentAssertions v8 | `Shouldly` (MIT) |

Kullanımda kalan büyük bağımlılıkların tamamı ücretsizdir: .NET/ASP.NET Core ve EF Core
(MIT), PostgreSQL (PostgreSQL License), Npgsql (PostgreSQL License), RabbitMQ (MPL 2.0),
FluentValidation (Apache 2.0), Serilog (Apache 2.0), OpenTelemetry (Apache 2.0),
Prometheus/Grafana OSS (Apache 2.0/AGPL, yalnızca yerel geliştirmede), xUnit (Apache
2.0), Testcontainers (MIT), Scalar (MIT).

## Sonuçlar

- Projenin klonlanıp çalıştırılması hiçbir lisans/ücret engeline takılmaz; CI dahil
  uçtan uca maliyet sıfırdır.
- "Framework X'i sürükle-bırak" yerine bazı altyapıyı (dispatcher, RabbitMQ katmanı)
  kendimiz yazdık; bu hem küçük bir ek maliyet hem de tasarımı derinlemesine anlatabilme
  kazancıdır.
- Ekosistem takibi gerekir: bir bağımlılık ileride lisans değiştirirse bu ADR'ye ek
  düşülür ve alternatife geçilir.
