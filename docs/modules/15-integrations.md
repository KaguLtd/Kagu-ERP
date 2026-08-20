# Entegrasyon Modülü

## 1. Amaç

Banka, KKTC e-fatura, e-posta/push, döviz kuru, yazar kasa/POS ve gelecekteki dış sistem bağlantılarını çekirdek iş mantığından ayıran güvenilir adaptör ve mesajlaşma katmanıdır.

## 2. Tasarım ilkeleri

- Dış sistem modeli doğrudan domain modeline sızmaz; anti-corruption mapper kullanılır.
- Her sağlayıcı ortak bir port/arayüzü uygular.
- Dış çağrı veritabanı transaction'ı içinde uzun süre bekletilmez.
- Transactional outbox iş olayı ile teslim niyetini atomik kaydeder.
- Gelen mesajlarda inbox/tekilleştirme anahtarı kullanılır.
- En az bir kez teslim varsayılır; tüketiciler idempotenttir.
- “Başarılı mı bilinmiyor” durumu açıkça modellenir ve sorgulanır.
- Tüm sözleşmeler sürümlü ve gözlemlenebilirdir.

## 3. Adaptör kataloğu

| Adaptör | İlk sürüm | Güvenli geri dönüş |
|---|---|---|
| KKTC e-fatura | portal veya onaylı API | doğrulanmış dosya + makbuz yükleme |
| Banka ekstresi | CSV/XLSX/MT formatına özel parser | manuel satır girişi + çift kontrol |
| Banka ödeme | onaylı dosya/API | bankada manuel işlem + referans kaydı |
| Döviz kuru | yapılandırılabilir güvenilir kaynak | yetkili manuel kur ve kanıt |
| E-posta | SMTP/API | uygulama içi bildirim |
| Android push | FCM benzeri adaptör | uygulama içi görev listesi |
| Yazar kasa/POS | resmi cihaz/satıcı protokolü | manuel günlük toplam/mutabakat |

Belirli marka veya resmi uç nokta, sözleşme ve onay olmadan varsayılmaz.

## 4. Outbox/inbox işleyişi

Outbox kaydı: olay türü/sürümü, aggregate kimliği, şirket, payload referansı, oluşturma zamanı, deneme sayısı, sonraki deneme, durum ve korelasyon kimliği.

Worker:

1. `FOR UPDATE SKIP LOCKED` benzeri güvenli kiralama ile işi alır.
2. Zaman aşımı ve devre kesici uygular.
3. Sağlayıcı idempotency anahtarını gönderir.
4. Sonucu/haricî kimliği kaydeder.
5. Geçici hatayı jitter'lı üstel gecikmeyle tekrarlar.
6. Kalıcı iş hatasını `requires_action`, üst sınırı aşan teknik hatayı dead-letter yapar.

Dead-letter kaydı silinmez; inceleme, tekrar oynatma ve gerekçe audit edilir.

## 5. Dosya entegrasyonları

- Yükleme karantina ve zararlı yazılım taramasından geçer.
- Dosya hash'i ve kaynağı saklanır; aynı dosya ikinci kez işlenmez.
- Parser sürümü ve satır numarası her sonucu izler.
- Ön izleme, hata raporu ve kullanıcı onayı olmadan finansal postalamaya geçilmez.
- Dışa aktarma karakter seti, tarih/ondalık biçimi ve kontrol toplamını belirtir.
- PII içeren dosya şifreli saklanır ve süreli bağlantıyla indirilir.

## 6. API/Webhook güvenliği

- TLS doğrulaması zorunlu; sertifika atlama yoktur.
- OAuth istemci bilgisi, API anahtarı ve imza anahtarı secret store'dadır.
- Gelen webhook imzası, zaman damgası ve tekrar saldırısı penceresi doğrulanır.
- IP allowlist tek başına kimlik doğrulama sayılmaz.
- Payload boyutu/şeması sınırlandırılır; hassas içerik loglanmaz.
- Anahtar rotasyonu kesintisiz çift anahtar penceresiyle yapılır.
- Test ve üretim kimlik bilgileri/endpoint'leri kesin ayrılır.

## 7. Sözleşme ve sürüm yönetimi

Her entegrasyon için:

- OpenAPI/XSD/dosya şeması ve örnekler,
- sağlayıcı hata sınıflandırması,
- zaman aşımı/yeniden deneme/idempotency davranışı,
- veri sahipliği ve mutabakat raporu,
- SLA/destek sahibi,
- veri konumu ve kişisel veri değerlendirmesi,
- uyumluluk testi ve sürüm yükseltme prosedürü

belgelenir. Kırıcı değişiklik yeni adaptör sürümüdür; sessiz güncelleme yapılmaz.

## 8. API ve operasyon ekranları

- `GET /api/v1/integrations/health`
- `GET /api/v1/integration-jobs`
- `POST /api/v1/integration-jobs/{id}/retry`
- `POST /api/v1/imports/{type}/preview`
- `POST /api/v1/imports/{id}/commit`

Kullanıcı ekranı teknik stack trace yerine iş anlamlı hata, etkilenen kayıt, sonraki adım ve korelasyon kodu gösterir. Operasyon ekranında kuyruk yaşı, hata oranı, son başarı, dead-letter ve sağlayıcı durumu bulunur.

## 9. Testler

- Sahte sağlayıcı/contract testleri ve kaydedilmiş resmi test örnekleri.
- Timeout, bağlantı kopması, 429, 5xx, bozuk/eksik yanıt.
- Yanıt gelmeden bağlantı kesildiğinde çift işlem oluşmaması.
- Aynı webhook/dosya/mesajın yeniden oynatılması.
- Şema sürümü, büyük payload ve kötü niyetli XML/CSV.
- Secret/log sızıntısı ve webhook tekrar saldırısı.
- Mutabakat: ERP sonucu ile dış sistem referanslarının tamlığı.
- Dead-letter yeniden oynatımında kaynak iş olayının tekrarlanmaması.

## 10. Kanonik belge ve staging sınırı

Dış veri doğrudan domain entity değildir. Her adaptör:

1. encrypted raw payload + content hash + provider/profile version saklar;
2. parser ile canonical staging record üretir;
3. type/reference/scope/currency/date ve kontrol toplamlarını doğrular;
4. preview/mapping sonucu verir;
5. onaylı idempotent business command çağırır;
6. domain result ile external reference arasında reconciliation kaydı üretir.

OASIS UBL, ISO 20022 camt, banka CSV/MT940 veya Logo export şemaları anti-corruption layer’da kalır. Provider alanının yokluğu domain invariant’ını zayıflatmaz; gerekli bilgi yoksa explicit validation/exception oluşur.

## 11. Belge ve banka standartları

- UBL Order/DespatchAdvice/ReceiptAdvice/Invoice/CreditNote referansları internal SourceLineLink’e map edilir; bire bir satır varsayılmaz.
- ISO 20022 camt.052/053/054 sürüm ve bankaya özel kullanım profiliyle parse edilir. Namespace/XSD seçimi dosyadan doğrulanır; “camt.053” tek sabit şema değildir.
- Banka statement opening/closing balance, sequence, booking/value date ve transaction reference kaybolmadan kanonik modele geçer.
- Her şema/profile version için fixture, XSD/semantic validation, backward compatibility ve quarantine test seti vardır.

KKTC resmi e-Fatura profili genel UBL’den ayrı adapter profile’dır; yalnız resmi şema ve onaylı örnekler production kabulü belirler.

## 12. Sıra, idempotency ve reconciliation

Outbox event; aggregate_id, aggregate_sequence, event_id, schema_version ve occurred_at taşır. Aynı aggregate sırası korunur; consumer event_id ile idempotenttir. Business transaction rollback olursa outbox yayınlanmaz. Publish sonrası işaretleme öncesi crash duplicate teslim yaratabilir; bu normal test senaryosudur.

MP-02 temeli `platform.outbox_message` ve `PostgresOutboxWriter` ile uygulanmıştır. Writer çağıranın mevcut DB connection/transaction'ına katılır ve commit açmaz. Aynı `event_id` + aynı canonical içerik no-op; aynı kimlikte farklı içerik veya aynı aggregate sequence için farklı event fail-closed olur. RLS tenant/company kapsamını zorlar. Worker leasing/publish/retry davranışı için kolon ve indeks sözleşmesi hazırdır; gerçek provider dispatch ve ayrı worker login rolü ilgili entegrasyon dikey diliminde tamamlanacaktır.

Inbound dedup yalnız file hash’e dayanmaz: provider + account/document type + external ID/sequence + version uygun unique anahtardır. Aynı dış ID farklı payload hash’iyle gelirse overwrite değil conflict/quarantine oluşur.

Dead-letter replay yalnız delivery/mapping aşamasını tekrarlar; posted source event, numara, payment, allocation veya GL yeniden oluşturulmaz. Operasyon ekranı source, attempt, last error class, next action, owner ve reconciliation state’i gösterir.

Entegrasyon tamamlanmış sayılması için sent count değil, accepted/settled/reconciled business result gerekir; dış sistem referansı ile ERP kaynak/alt defter sonucu tamlık raporunda eşleşir.
