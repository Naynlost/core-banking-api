# ADR 0005 — MediatR yerine kendi hafif CQRS dispatcher'ımız

**Durum:** Kabul edildi (Aşama 3)

## Bağlam

Command/query ayrımı ve "bir istek → bir handler" modeli isteniyor. .NET dünyasında
bunun fiili standardı MediatR'dı; ancak v13 ile **ticari lisansa** geçti (bkz. ADR 0009).
Ayrıca MediatR'ın sağladığı çekirdek işlev — DI container'dan doğru handler'ı bulup
çağırmak — küçük bir kod parçasıdır; bunun için dış bağımlılık taşımak gerekmez.

## Karar

Kendi hafif dispatcher'ımızı yazdık (Banking.Application içinde, dış bağımlılık sıfır):

- `ICommand` / `ICommand<TResponse>` / `IQuery<TResponse>` işaret arayüzleri;
  command/query'ler `record`.
- `ICommandHandler<T>` / `IQueryHandler<T,R>` handler sözleşmeleri; hepsi
  `CancellationToken` alır, `Result` döndürür.
- `Dispatcher`, handler tipini istek tipinden çözer, çözümü **cache'ler** ve DI
  scope'undan alıp çağırır. `AddApplication` assembly-scan ile tüm handler'ları kaydeder.
- Pipeline ihtiyaçları wrapper'larla karşılanır: FluentValidation validator'ları
  handler'dan önce koşar; dispatcher her isteği bir OpenTelemetry span'ine
  (`handle X`, `banking.outcome` tag'i) sarar.

## Değerlendirilen alternatifler

- **MediatR v12'ye sabitlenmek:** çalışır, ama sürüm tavanı taşımak ve bir gün zorunlu
  geçiş riski almak demek.
- **Handler'ları controller'dan doğrudan DI ile almak:** en yalın seçenek; fakat
  validation/telemetry gibi kesişen davranışları her controller'da tekrarlatır.

## Sonuçlar

- Sıfır lisans riski, sıfır dış bağımlılık; davranışın tamamı ~1 dosyada okunabilir
  ve testlidir (dispatcher'ın kendisi ve validation pipeline'ı birim testlidir).
- MediatR'ın notification/streaming gibi ileri özellikleri yoktur; ihtiyaç olursa
  bilinçli olarak eklenir, bedavaya gelen sihir yoktur — bu, tercih edilen bir takastır.
