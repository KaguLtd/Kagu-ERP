# Test ve Kalite Stratejisi

## 1. Amaç

ERP'nin “ekran açılıyor” düzeyinde değil; mali doğruluk, yetki izolasyonu, eşzamanlılık, mevzuat uyumu, geri yüklenebilirlik ve gerçek kullanıcı akışları açısından güvenilir olduğunu kanıtlayan test sistemini tanımlar.

Test piramidi tek başına yeterli model değildir. Her gereksinim şu kanıt türlerinden uygun olanlarına bağlanır: domain unit/property, veritabanı entegrasyon, API contract, istemci bileşen, uçtan uca, güvenlik, performans, operasyon ve manuel uzman kabulü.

## 2. Kalite kapıları

| Kapı | Zorunlu kanıt |
|---|---|
| Pull request | format/lint, compile, unit, hızlı integration, secret/dependency scan |
| Ana dal | tüm integration, OpenAPI/DB uyumluluk, web E2E smoke, Android unit/lint |
| Release candidate | tam E2E, güvenlik, migration, performans, yedek/restore örneği |
| Pilot | kullanıcı kabulü, mali mutabakat, gözlemlenebilirlik, destek runbook'u |
| Üretim | imzalı kontrol listesi, geri dönüş planı, hukuk/mali müşavir kapıları |

Başarısız veya atlanmış test “yeşil” sayılmaz. Zorunlu istisna; sahibi, riski, telafisi ve son kullanma tarihi olan yazılı karardır.

### 2.1 Geliştirme kadansı — dar dilim ve MP sonu regresyon

`TEST-POL-001` / `DEC-MP01-024` uyarınca test sıklığı risk ve master fazı kapısına göre iki seviyelidir:

- Dikey dilimde yalnız değişen davranışın domain/property testi, gerekiyorsa gerçek PostgreSQL constraint/RLS/concurrency testi ve ilgili compile/static kapı çalışır. Finansal veya authorization invariantı sonraki MP sonuna kadar tamamen testsiz bırakılmaz.
- Her oturumda solution-wide build, bütün DB harness'i, restore, web, Android ve bütün golden paket tekrarlanmaz.
- İlgili MP `validating` durumuna girerken full locked restore, solution build/format, tüm unit/integration, gerçek PostgreSQL/RLS/concurrency, boş+mevcut DB migration, restore, golden cross-foot, web/Android ve güvenlik taramaları tek birleşik kapanış paketi olarak çalışır.
- Kapanış paketindeki bulgular aynı MP içinde düzeltilir ve etkilenen dar testten sonra bütün kapanış paketi yeniden çalıştırılır. MP ancak son birleşik koşu yeşilken `completed` olabilir.
- Ortam engeli veya ertelenen test açık risk, neden ve tekrar koşu tetikleyicisiyle görev planında kalır; başarı sayılmaz.

Console tabanlı domain unit harness, MP içindeki dar koşular için bir veya daha fazla test-adı filtresi kabul eder (örnek: `dotnet run --project tests/Unit/KaguERP.DomainUnitChecks.csproj -- "opening"`). Filtresiz çağrı bütün unit paketini çalıştırır ve MP kapanış davranışıdır.

## 3. Test katmanları

### 3.1 Domain unit ve property testleri

- Para, miktar, kur, KDV, iskonto ve yuvarlama.
- Belge durum makineleri ve izinli/yasak geçişler.
- Çift taraflı kayıt: üretilen her fişte borç = alacak.
- Stok: hareketlerin toplamı bakiye ile eşleşir; seri tekilliği.
- Cari kapama: tahsis toplamı belge ve ödeme sınırını aşmaz.
- İş akışı: limit, para birimi ve görevler ayrılığı.
- Yürürlük tarihli kural seçiminde sınır zamanlar.

Property/fuzz testleri negatif, sıfır, çok büyük, çok satırlı ve yuvarlama uçlarını üretir.

### 3.2 Veritabanı entegrasyon testleri

Gerçek PostgreSQL container'ı kullanılır; EF in-memory veya SQLite mali davranış kanıtı sayılmaz.

- constraint, unique, foreign key ve RLS,
- transaction/rollback ve outbox atomikliği,
- paralel sipariş/numara/ödeme/idempotency,
- migration ileri ve desteklenen geri dönüş,
- query plan/indeks kritik sorguları,
- tenant/şirket izolasyonu ve connection pool bağlam temizliği.

Her test izole veritabanı/şema veya güvenli temizleme stratejisi kullanır; sıra bağımlılığı yoktur.

### 3.3 API ve sözleşme

- OpenAPI şema doğrulama ve kırıcı değişiklik denetimi.
- Authentication/authorization negatif matrisi.
- Problem Details hata kodları ve alan hataları.
- Idempotency aynı anahtar/aynı payload ve aynı anahtar/farklı payload.
- ETag/`If-Match` çatışması.
- Sayfalama, sıralama, filtre allowlist ve limitler.
- Tüketici sözleşmeleri: web, Android ve entegrasyon worker.

### 3.4 Web ve Android

Web: kullanıcı davranışı odaklı bileşen testi; Playwright ile kritik akış, erişilebilirlik ve tarayıcılar. Android: ViewModel/repository/sync, Compose semantics ve gerçek/emüle cihaz.

İstemci testleri sunucu yetkisini taklit etmekle yetinmez; en az kritik E2E'ler gerçek auth/API/PostgreSQL kullanır.

### 3.5 Entegrasyon ve sağlayıcı testleri

- Fake server ile hata/timeout/duplicate senaryoları.
- Sağlayıcının resmi sandbox/sertifikasyon paketi.
- XSD/Schematron, banka dosya örnekleri ve golden payload.
- Mutabakat: dış referans–ERP kaydı tamlığı.

## 4. Altın mali veri seti

Sürüm kontrollü, anonim/sentetik bir “golden company” hazırlanır:

- en az iki şube/depo, iki para birimi,
- müşteri/tedarikçi, iskonto, iade, avans,
- lot/seri ve negatif stok sınırı,
- satın alma kabul/fatura farkı,
- banka/çek/tahsilat,
- kur farkı ve dönem kapanışı,
- KDV/e-fatura olumlu ve istisna örnekleri.

Beklenen mizan, cari, stok, KDV ve rapor toplamları mali müşavir tarafından onaylanan sabit fixture olarak saklanır. Her release candidate otomatik mutabakat yapar.

## 5. Test verisi ve gizlilik

- Üretim verisi geliştirici/test ortamına kopyalanmaz.
- Gerekirse onaylı maskeleme/tokenization ve erişim kaydı uygulanır.
- Fixture'lar deterministik; tarih ve saat için test clock kullanılır.
- VKN, IBAN ve belge numarası sentetik ama biçimsel geçerlidir.
- Secret ve gerçek servis credential'ı test deposunda bulunmaz.
- Test sonrası hassas artefaktlar saklama politikasına göre temizlenir.

## 6. Eşzamanlılık ve hata enjeksiyonu

Zorunlu senaryolar:

- son stok için iki paralel rezervasyon,
- aynı e-fatura seri numarası için iki istek,
- aynı ödeme/fişi çift tıklama/yeniden deneme,
- postala sırasında process kill,
- DB commit başarılıyken dış çağrı başarısız,
- worker aynı outbox mesajını iki kez alıyor,
- ağ yanıtı kayboluyor ve sonuç belirsiz,
- failover/restore sonrası işlerin yeniden başlaması.

Sonuç: çift finansal kayıt yok; kullanıcıya dürüst durum; yeniden çalışma idempotent.

## 7. Güvenlik testi

[Güvenlik dokümanındaki](02-security-and-threat-model.md) kontrol setine ek:

- OWASP ASVS 5.0 Level 2 izlenebilirliği,
- SAST, SCA, secret, IaC/container image taraması,
- authz/RLS otomatik negatif testleri,
- XSS, CSRF, SSRF, injection, dosya yükleme,
- rate-limit ve brute-force,
- OIDC redirect/logout/session fixation,
- Android depolama, backup, deep link ve repackaging incelemesi,
- bağımsız sızma testi: pilot öncesi ve önemli güvenlik değişiminde.

## 8. Performans ve dayanıklılık

Yük testleri [performans planındaki](03-performance-and-capacity.md) temsilî veri ve eşzamanlılıkla çalışır. Restore/DR testi [kurtarma planının](../operations/02-backup-restore-disaster-recovery.md) kalite kapısıdır. E-fatura ve banka sağlayıcı kesintileri chaos/fault injection ile simüle edilir.

## 9. Regresyon ve hata yönetimi

- Bulunan her üretim/pilot hatası önce başarısız tekrarlanabilir teste dönüştürülür.
- Flaky test karantinada unutulmaz; sahibi ve son tarihi vardır.
- Retry yalnız çevresel kararsızlığı gizlemeyecek tanımlı yerlerde.
- Screenshot farkı otomatik onaylanmaz; tasarım ve iş anlamı incelenir.
- Code coverage risk göstergesidir, hedef değildir; finansal invariant ve kritik dallar açıkça listelenir.

## 10. Test ortamları

- Geliştirici: container'lı PostgreSQL/Keycloak/fake providers.
- CI: her iş için izole ve kısa ömürlü ortam.
- Staging: üretime yakın topology/config, sentetik veri, gerçek sandbox entegrasyonları.
- Restore lab: üretim yedeğinin yetkili, izole ve auditli geri yükleme ortamı.

Prod dışı hiçbir ortam prod e-posta/ödeme/e-fatura hedeflerine varsayılan olarak ulaşamaz.

## 11. Gereksinim izlenebilirliği

Her gereksinim kimliği (`ACC-INV-001` vb.) en az bir test kimliğine bağlanır. Test raporu; build, commit, migration, test veri sürümü, ortam ve sonucu taşır. Hukuki gereksinimlerde ayrıca resmi kaynak ve uzman kabulü bulunur.

## 12. Çıkış ölçütleri

- Kritik/yüksek açık hata yok.
- Golden mali veri sıfır beklenmeyen farkla mutabık.
- Kritik E2E ve güvenlik matrisi yeşil.
- Performans SLO/bütçeleri sağlanmış.
- En güncel geri yükleme tatbikatı RPO/RTO içinde başarılı.
- Pilot kullanıcı ve mali müşavir kabulü kayıtlı.
- Kalan riskler tarihli ve sorumlusu belirli.

## 13. Muhasebe döngüsü golden senaryoları

Golden veri yalnız tek fiş örneği değil, süreç döngüsüdür. Her senaryo kaynak belgeler, expected subledger entries, allocations, journal lines, reports ve reversal sonucunu sürümlü JSON/fixture ile tanımlar:

1. order-to-cash: kısmi sevk, birleşik fatura, üç taksit, kısmi payment/allocation, statement reconciliation, iade;
2. procure-to-pay: kısmi receipt, GRNI, price/quantity variance, invoice, payment hold/release, bank settlement;
3. inventory: transfer, lot/serial, blind count, adjustment, backdated receipt ve valuation repost;
4. instruments: received cheque, allocation, endorsement/bank presentation, clear/dishonour;
5. record-to-report: accrual, FX valuation, control account reconciliation, trial balance, statements, lock/reopen.

Her expected satır source event, effective date, currency/rate, rule version ve dimension ile elle/uzman incelemesinden geçer. Snapshot değişimi “update golden” komutuyla kör kabul edilmez; fark raporu ve muhasebe onayı ister.

## 14. Property ve metamorphic kontroller

- Her booked transaction için debit = credit.
- Her control account için subledger = GL, aynı as-of/scope/currency’de.
- Payment allocations usable payment ve open-item original amount sınırını aşmaz.
- Allocation + unallocation net sıfırdır; kaynak payment/GL değişmez.
- Transfer signed quantity toplamı sıfır; count adjustment açık source taşır.
- Reversal, tanımlı kapsamda asıl etkinin negatifidir ve aslı silmez.
- Aynı idempotency key aynı sonuç; farklı payload conflict.
- Projection rebuild aynı source/rule ile aynı checksum; dış side effect üretmez.
- Rapor satırları subtotal/grand total’a cross-foot; drill-down toplamı üst satıra eşit.

Farklı komut sıraları aynı iş gerçeğine götürüyorsa sonuç invariantları eşit olmalıdır; örneğin tek payment’ın iki allocation sırası bakiyeyi değiştirmez.

## 15. Cut-off, zaman ve eşzamanlılık testleri

- midnight/DST Europe/Nicosia, ay/yıl sonu ve leap day;
- document/effective/recorded/posted tarih ayrımı;
- period/tax/inventory lock ile yarışan posting;
- backdated stock event sonrası etkilenen cost layers;
- rapor pagination sırasında concurrent posting ve as-of token;
- statement import duplicate/conflicting payload;
- approval input değişirken eşzamanlı karar;
- worker publish/ack arasında crash ve duplicate delivery.

Fault injection DB deadlock, provider timeout/accepted-but-no-response, disk full, object store unavailable ve worker restart koşullarında yarım belge/ledger üretmediğini kanıtlar.

## 16. Kullanıcı ve kontrol kabulü

UAT script’i role göre gerçek işi ölçer: satış, depo, satın alma, finans, muhasebe, yönetici ve denetçi. Sonuç sadece pass/fail değil; süre, kritik hata, workaround, yardım, kontrol kanıtı ve güven ölçüsüdür.

İki paralel kapanışta eski sistem/uzman çalışma dosyasıyla belge sayısı, alt defter, GL, vergi, stok değer ve mali tablo karşılaştırılır. Farkların owner/reason/disposition kaydı olmadan go-live olmaz.
