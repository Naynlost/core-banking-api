# Canlı demo yayınlama (tamamı ücretsiz katman)

Amaç: işe alım sürecinde birinin `docker compose` kurmadan tıklayabileceği bir demo.
Üç ücretsiz servis yeter; kredi kartı gerektirmeyen kombinasyon:

| Bileşen | Servis | Ücretsiz katman |
|---|---|---|
| API (Docker) | [Render](https://render.com) Web Service | 750 saat/ay, boşta uyur (ilk istek ~30 sn) |
| PostgreSQL | [Neon](https://neon.tech) | 0,5 GB, sınırsız süre |
| RabbitMQ | [CloudAMQP](https://www.cloudamqp.com) "Little Lemur" | 1M mesaj/ay |

## 1. Neon — PostgreSQL

1. Neon'da proje aç, `banking` veritabanı oluştur; connection string'i kopyala.
2. Şemayı yerelden uygula (uygulama açılışta migration çalıştırmaz):

   ```bash
   ASPNETCORE_ENVIRONMENT=Development Jwt__Secret="gecici-en-az-32-byte-secret-123456" \
   ConnectionStrings__BankingDb="Host=<neon-host>;Database=banking;Username=<user>;Password=<pass>;SSL Mode=Require" \
     dotnet ef database update -p src/Banking.Infrastructure -s src/Banking.Api
   ```

## 2. CloudAMQP — RabbitMQ

1. "Little Lemur" (free) instance aç; panelden host, kullanıcı, parola ve vhost değerlerini al.
2. CloudAMQP **AMQPS (TLS, port 5671)** ister ve vhost genellikle kullanıcı adıyla aynıdır —
   uygulama bunları `RabbitMq__UseTls=true`, `RabbitMq__Port=5671`, `RabbitMq__VirtualHost=<vhost>`
   ayarlarıyla destekler.

## 3. Render — API

1. GitHub repo'sunu bağla; "Web Service" → Runtime: **Docker** (repo kökündeki `Dockerfile`).
2. Environment değişkenleri:

   | Değişken | Değer |
   |---|---|
   | `ASPNETCORE_HTTP_PORTS` | `8080` (Dockerfile'ın dinlediği port; Render otomatik yönlendirir) |
   | `ConnectionStrings__BankingDb` | Neon connection string (`SSL Mode=Require` dahil) |
   | `Jwt__Secret` | en az 32 byte'lık rastgele bir değer |
   | `RabbitMq__HostName` | CloudAMQP host |
   | `RabbitMq__Port` | `5671` |
   | `RabbitMq__UserName` / `RabbitMq__Password` | CloudAMQP panelinden |
   | `RabbitMq__VirtualHost` | CloudAMQP vhost (genelde kullanıcı adı) |
   | `RabbitMq__UseTls` | `true` |
   | `FraudReview__ReviewerEmails__0` | (opsiyonel) fraud inceleme rolü verilecek e-posta |

3. Health check path: `/health/ready`.
4. Deploy sonrası `https://<app>.onrender.com/scalar/v1` sadece Development'ta açık olduğundan
   prod'da kapalıdır; demo için Scalar'ı açık bırakmak istersen `ASPNETCORE_ENVIRONMENT=Development`
   ver (demo verisi için kabul edilebilir bir kısayol) veya Program.cs'teki koşulu kaldır.

## Notlar

- **OpenTelemetry:** Cloud'da Jaeger olmadığından OTLP exporter hedef bulamaz ve sessizce
  denemeye devam eder; zararsızdır. İstersen `OTEL_TRACES_EXPORTER=none` ile kapat.
- **Uyuma:** Render free instance boşta uyur; ilk istek ~30 sn sürer. README'deki demo
  linkinin yanına bunu not düş.
- **Maliyet:** Üç servis de kart istemez; kota aşımında servis durur, fatura çıkmaz.
