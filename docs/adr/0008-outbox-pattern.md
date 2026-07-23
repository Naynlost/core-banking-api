# ADR 0008: Güvenilir olay yayını için outbox + inbox + DLQ

**Durum:** Kabul edildi (Aşama 5, DLQ Aşama 9)

## Bağlam

Transfer sonrası olay (bildirim, fraud taraması) yayınlanmalı. Naif yaklaşım, DB
commit'inden sonra kuyruğa publish etmektir; bu **dual-write** problemidir:

- Commit başarılı + publish başarısız → olay sonsuza dek kayıp (fraud taraması hiç
  çalışmaz).
- Publish başarılı + commit başarısız → hiç olmamış bir transferin olayı yayınlanır.

İki sistemi (DB + broker) tek atomik işlemde birleştirmek mümkün değildir; dağıtık
transaction (2PC) ise RabbitMQ'da desteklenmez ve operasyonel olarak ağırdır.

## Karar

**Outbox pattern:** olay, transferi yazan **aynı DB transaction'ında** `outbox_messages`
tablosuna yazılır. Tek sistem, tek commit: transfer varsa olay kaydı da vardır.

- `OutboxPublisher` (BackgroundService) bekleyen satırları periyodik toplar ve
  RabbitMQ `banking.events` topic exchange'ine **publisher confirm** ile basar; satır
  ancak broker onayından sonra `processed_at` alır. Uygulama çökse bile bekleyen satır
  kalır ve yeniden başlatmada yayınlanır; teslim garantisi **at-least-once**.
- At-least-once tekrar üretebilir; consumer tarafında `inbox_messages` tablosu
  (consumer başına dedupe) tekrarları ayıklar → **effectively-once** işleme.
- İşlenemeyen mesaj bir kez requeue edilir; ikinci hatada **dead-letter** exchange'i
  üzerinden `banking.dead-letters` kuyruğuna parkedilir: poison mesaj ne kuyruk başını
  tıkar ne de kaybolur; elle incelenebilir.
- Correlation id ve W3C `traceparent` outbox satırında ve AMQP header'larında taşınır:
  HTTP isteğinden consumer'a kadar tek trace / tek correlation id.
- Yayınlanmış outbox ve inbox kayıtları 7 gün sonra retention job'ı ile temizlenir;
  bekleyen outbox satırı asla silinmez.

## Sonuçlar

- Olay kaybı ve hayalet olay sınıf olarak yok edilir; integration testi "restart"
  senaryosunu kanıtlar (olayı yazan süreç kapatılır, yenisi bekleyeni yayınlar).
- Bedeli gecikme (polling aralığı kadar) ve şemaya iki tablo eklenmesidir; bankacılık
  olayları için saniye altı gecikme fazlasıyla kabul edilebilir.
- Consumer'lar idempotent olmak zorundadır; bu bir kısıt değil, dağıtık sistemlerde
  zaten kaçınılmaz olan gerçeğin açıkça kabulüdür.
