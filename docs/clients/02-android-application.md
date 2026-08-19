# Android Mobil Uygulama Geliştirme Şartnamesi

## 1. Amaç ve kapsam

Android uygulaması web ERP'nin küçük ekrana kopyası değildir. İlk fazda saha ve yönetim görevlerine odaklanır:

- onay görevleri ve güvenli belge özeti,
- cari/stok/fiyat ve belge görüntüleme,
- depo sayım/barkod işlemleri,
- tahsilat/teslim kanıtı taslağı,
- bildirim ve görev takibi,
- yönetim göstergeleri.

Muhasebe kural yönetimi, dönem kapatma, toplu banka ödemesi ve ayrıntılı rapor tasarımı webde kalır. Mobil kapsam ürün kararıyla genişletilir.

## 2. Teknoloji kararı

- Kotlin,
- Jetpack Compose + Material 3/adaptive layout,
- tek Activity, Navigation Compose,
- katmanlı Android mimarisi ve unidirectional data flow,
- Hilt dependency injection,
- Room yerel veri tabanı,
- WorkManager kalıcı eşitleme işleri,
- Retrofit/OkHttp veya OpenAPI'den üretilen tipli istemci,
- Kotlin Coroutines + Flow,
- Android Keystore ve sistem kimlik doğrulama tarayıcısı,
- JUnit, Turbine, Compose UI test, MockWebServer ve cihaz testleri.

Önerilen ilk `minSdk` 29'dur. Firmanın barkod cihazı parkında daha eski Android varsa envanter çalışması sonrası ADR ile düşürülür; güvenlik güncellemesi almayan cihazlar üretim erişimine kabul edilmez.

## 3. Güven sınırı ve kimlik doğrulama

- Android istemci hiçbir koşulda PostgreSQL'e doğrudan bağlanmaz; yalnız HTTPS API kullanır.
- OIDC Authorization Code + PKCE, sistem tarayıcısı/Custom Tabs ile uygulanır; gömülü WebView giriş ekranı kullanılmaz.
- Uygulama public client'tır; içine client secret gömülmez.
- Tokenlar düz SharedPreferences, log, yedek veya ekran görüntüsünde saklanmaz; Keystore destekli güvenli depolama kullanılır.
- Sunucu kısa ömürlü access token, kontrollü refresh rotation ve cihaz/oturum iptali uygular.
- Yüksek riskli onay/tahsilat öncesi yeniden doğrulama veya biyometrik cihaz onayı istenebilir; biyometri sunucu yetkisinin yerine geçmez.
- Sertifika pinning ancak rotasyon ve acil durum planı ADR ile çözülür; platform TLS doğrulaması asla devre dışı bırakılmaz.

## 4. Proje yapısı

```text
apps/android/
├── app/
├── core/
│   ├── model/
│   ├── network/
│   ├── database/
│   ├── auth/
│   ├── designsystem/
│   ├── telemetry/
│   └── testing/
├── feature/
│   ├── approvals/
│   ├── inventory/
│   ├── parties/
│   ├── documents/
│   └── dashboard/
└── sync/
```

UI, domain ve data katmanları ayrılır. ViewModel UI state üretir; composable doğrudan ağ/Room çağırmaz. Repository tek veri erişim kapısıdır.

## 5. Çevrim dışı yaklaşım

İlk sürüm **offline-read-first** çalışır:

- Başarılı sunucu verileri gerekli kapsam ve TTL ile Room'a yazılır.
- UI yerel veriyi gözlemler; repository ağdan yeniler.
- Ekran son yenilenme zamanı ve çevrim dışı durumunu açıkça gösterir.
- Şirket değişiminde cache mantıksal/fiziksel olarak izole edilir.
- Oturum iptali veya kullanıcı değişiminde hassas cache temizlenir.

Çevrim dışı yazma yalnız sayım/taslak gibi açıkça seçilmiş komutlarda ikinci fazdır. Resmi fatura numarası, finansal postala, ödeme onayı ve dönem kapatma çevrim dışı kuyruğa alınmaz.

## 6. Güvenli kuyruklu komut modeli

İzin verilen offline komut:

- istemci UUID/idempotency anahtarı,
- aggregate kimliği ve beklenen `version`/ETag,
- şirket/kullanıcı kapsamı,
- oluşturma ve son geçerlilik zamanı,
- şifreli minimal payload,
- kullanıcıya görünür durum

taşır. `queued → sending → acknowledged` veya `conflict/failed/expired` durumları bulunur. WorkManager ağ geldiğinde gönderir. `409` çatışması sessiz birleştirilmez; kullanıcı güncel veriyi görüp yeniden karar verir.

## 7. Senkronizasyon

- Delta endpoint/cursor, sayfalı ve idempotenttir.
- Silinen/erişimi kaldırılan kayıtlar tombstone ile yerel cache'den çıkarılır.
- Sunucu zamanı ve ETag esas alınır; cihaz saati iş kuralı kaynağı değildir.
- Büyük kataloglar parça parça ve yalnız gerekli alanlarla senkronize edilir.
- Retry yalnız geçici ağ/5xx/429 hatalarında, jitter'lı backoff ile yapılır.
- 401/403 otomatik sonsuz retry değildir; kimlik/izin akışına döner.
- Push “yenile” sinyalidir; içinde hassas veri veya yetkili iş sonucu bulunmaz.

## 8. Mobil UX

- Alt gezinme en fazla 3–5 üst görev; ayrıntı adaptive list-detail kalıbı.
- Tek elle ana eylem, fakat riskli işlemlerde açık özet ve onay ekranı.
- Barkod taramada manuel giriş ve izin reddi geri dönüşü.
- Ağ/çevrim dışı/son senkron durumu üst düzeyde görünür.
- Para ve belge durumu yalnız renkle anlatılmaz.
- Tablet/geniş ekranda iki panel; telefon ekranında ardışık gezinme.
- Dinamik yazı tipi, ekran okuyucu, dokunma hedefi ve yatay/dikey yön test edilir.

## 9. Cihaz ve veri güvenliği

- `android:allowBackup` ve veri çıkarma kuralları hassas yerel veriyi bulut/ADB yedeğinden dışlar.
- Root/debug/emülatör tespiti tek başına güvenlik kararı değildir; risk sinyali olarak ele alınır.
- Üretim build'i debuggable değildir; loglar PII/token içermez.
- Ekran görüntüsü engeli yalnız gerçekten hassas ekranlarda uygulanır; erişilebilirlik etkisi değerlendirilir.
- İndirilen belge özel app storage'da ve süreli tutulur; genel Downloads'a sessizce yazılmaz.
- Deep link allowlist ve oturum/yetki doğrulaması kullanır.
- Uygulama imzalama anahtarı geliştirici makinesinde tutulmaz; kontrollü CI/Play App Signing süreci kullanılır.
- MDM/uzaktan silme gereksinimi firma cihaz politikasında kararlaştırılır.

## 10. API hata ve telemetri

Ortak hata sözleşmesi `type`, `title`, `status`, `code`, `correlationId`, `fieldErrors` alanlarını işler. Teknik stack trace kullanıcıya gösterilmez. Crash/performance telemetrisi kullanıcı/şirket takma kimliğiyle ve açık veri sınıflandırmasına göre gönderilir; finansal içerik gönderilmez.

## 11. Test matrisi

- Unit: ViewModel, use case, mapper, para/tarih kuralları.
- Repository: Room + MockWebServer, cache/yenileme davranışı.
- Sync: süreç ölümü, ağ gidip gelmesi, retry, duplicate, tombstone, 409.
- UI: Compose semantics, font scaling, dark/light, telefon/tablet.
- Güvenlik: token storage, backup, deep link, log, ekran/switch account.
- E2E: giriş, MFA, onay, barkod sayım, çevrim dışı görüntüleme ve çıkışta temizleme.
- Cihaz: desteklenen en düşük/son Android, düşük bellek, kötü ağ ve üreticiye özgü pil kısıtları.

## 12. Dağıtım

İlk tercih yönetilen Google Play/kapalı test kanalıdır; Google servislerinin firma/ülke koşullarında uygunluğu doğrulanır. Alternatif kurumsal dağıtım, imza/güncelleme zorlaması ve cihaz güvenliği planı olmadan APK paylaşımına dönüştürülmez.

Her sürümde sürüm kodu, Git commit, API uyumluluk aralığı, migration, SBOM/bağımlılık taraması ve rollback/önceki destekli sürüm politikası kayıtlıdır.

## 13. Mobil için tamamlanmış sayılma

- Desteklenen cihazlarda kimlik/PKCE ve oturum iptali doğrulanmış.
- Çevrim dışı/cache davranışı ve hassas veri temizliği test edilmiş.
- API sözleşmesi ve idempotency testleri geçmiş.
- Erişilebilirlik/adaptive layout testleri geçmiş.
- Crash-free pilot, performans ve pil/ağ bütçeleri kabul edilmiş.
- Play/kurumsal dağıtım, imza, gizlilik ve destek sorumluları tanımlanmış.

## 14. Mobil görev sınırı

Android, ilk sürümde sorgu/onay ve kontrollü saha kanıtı istemcisidir. Aşağıdaki işler çevrim dışı kesinleşemez:

- GL posting/manual journal;
- payment/bank reconciliation onayı;
- period reopen/close veya repost;
- e-Fatura issue/send/cancel;
- kritik master data ve banka hesabı değişikliği.

Offline taslak; client-generated command ID, captured-at/device timezone, scope, attachment hash ve expected server version taşır. Sunucuya gelince permission, current state, lock, rule ve idempotency yeniden değerlendirilir. “Cihazda başarılı” yalnız queued demektir; server accepted/posted ile karıştırılmaz.

## 15. Depo sayım ve saha akışı

Mobil barkod/lot/seri tarama CountPlan ve watermark’a bağlıdır. Blind count modunda expected quantity API’den cihaza gönderilmez. Aynı item/location tekrar taranırsa kullanıcıya birleştirme veya ayrı lot/seri nedeni gösterilir.

Senkronizasyonda count sırasında oluşan transfer/receipt listesi conflict olarak gelir; uygulama otomatik fark adjustment’ı üretmez. Tolerans üstü fark recount/approval görevi açar. Fotoğraf veya belge kanıtı cihaz galerisinde gereksiz kalmaz; şifreli geçici dosya upload/ack sonrası policy’ye göre silinir.

## 16. Mobil durum ve güven kanıtı

Her finansal kart business, accounting, allocation ve bank/integration durumlarını ayrı etiketler; offline/stale bilgi belirgin as-of zamanı taşır. Approver, karar anında tutar/currency, karşı taraf, banka/IBAN maskesi, değişiklik özeti, policy ve kendi SoD uygunluğunu görür.

Test matrisi:

- offline duplicate command ve process death sonrası idempotent resume;
- token/scope değişirken queued command reddi;
- blind-count bilgi sızıntısı;
- approval input değişince stale/reset;
- remote wipe/logout sonrası local financial cache ve attachment temizliği;
- düşük ağda queued/accepted/posted ayrımının doğru gösterimi.
