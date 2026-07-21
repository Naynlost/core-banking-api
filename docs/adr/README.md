# Mimari Karar Kayıtları (ADR)

Bu klasör, projedeki önemli mimari kararları ve gerekçelerini belgeler. Her kayıt,
kararın alındığı andaki bağlamı, tartılan seçenekleri ve kabul edilen sonuçları
(trade-off'lar dahil) içerir. Kararlar değişirse eski kayıt silinmez; yeni bir ADR
eskisini "geçersiz kıldı" olarak işaretler.

| No | Karar | Durum |
|---|---|---|
| [0001](0001-clean-architecture.md) | Clean Architecture ve içe doğru bağımlılık | Kabul edildi |
| [0002](0002-double-entry-ledger.md) | Tek bakiye kolonu yerine çift taraflı defter | Kabul edildi |
| [0003](0003-money-decimal.md) | Para için `double` değil `decimal` tabanlı `Money` value object'i | Kabul edildi |
| [0004](0004-result-pattern.md) | İş kuralı hatalarında exception değil Result pattern | Kabul edildi |
| [0005](0005-custom-cqrs-dispatcher.md) | MediatR yerine kendi hafif CQRS dispatcher'ımız | Kabul edildi |
| [0006](0006-idempotency-key.md) | Para hareketlerinde zorunlu `Idempotency-Key` | Kabul edildi |
| [0007](0007-optimistic-locking.md) | Hesap versiyonu ile optimistic locking | Kabul edildi |
| [0008](0008-outbox-pattern.md) | Güvenilir olay yayını için outbox + inbox + DLQ | Kabul edildi |
| [0009](0009-free-oss-only.md) | Yalnızca ücretsiz ve açık kaynak bağımlılıklar | Kabul edildi |
| [0010](0010-multi-currency-fx.md) | Çapraz kur: para birimi başına denge ve FX pozisyon hesapları | Kabul edildi |
