# ADR 0003 — Para için `double` değil `decimal` tabanlı `Money` value object'i

**Durum:** Kabul edildi (Aşama 1)

## Bağlam

`double`/`float` ikili (binary) kayan noktadır; 0.1 gibi onluk kesirleri tam temsil
edemez. `0.1 + 0.2 != 0.3` sınıfı hatalar, milyonlarca işlemde birikerek gerçek para
kaybına dönüşür. Ayrıca çıplak sayı kullanmak tutar ile para birimini birbirinden
koparır: TRY ile EUR'yu toplamak derleyici için sıradan bir toplamadır.

## Karar

- Tutarlar **`decimal`** tutulur (onluk tabanda tam temsil; bankacılık aralığı için
  fazlasıyla yeterli hassasiyet). Veritabanında `numeric(18,2)` kolonuna eşlenir.
- Tutar tek başına dolaşmaz: **`Money` value object'i** tutar + `Currency` taşır ve
  tüm aritmetik oradadır. Farklı para birimli iki `Money` toplanamaz/karşılaştırılamaz;
  ihlal domain hatası üretir.
- `Money`, EF Core'da complex property olarak iki kolona (`amount`, `currency`) açılır;
  ayrı tablo/join maliyeti yoktur.

## Değerlendirilen alternatif

Tam sayı kuruş (`long`) tutmak da doğrudur ve bazı sistemler tercih eder. `decimal`
seçildi çünkü .NET'te ilk sınıf destek görür, `numeric` kolonuna doğal eşlenir ve
kuruş-çevrim hatası diye bir sınıf hata baştan yok olur. İkisi arasındaki seçim
önemli değildir; önemli olan **asla ikili kayan nokta kullanmamaktır**.

## Sonuçlar

- Yuvarlama sürprizi yoktur; testlerde tutarlar birebir karşılaştırılabilir.
- Para birimi karışıklığı derleme/çalışma zamanında engellenir; "100" tek başına
  anlamlı bir tip değildir.
- Bedeli önemsiz düzeyde performanstır (`decimal` aritmetiği `double`'dan yavaştır);
  finansal doğruluk karşısında tartışma konusu bile değildir.
