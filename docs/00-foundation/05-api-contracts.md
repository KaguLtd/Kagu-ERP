# 05 — API ve Entegrasyon Sözleşmesi Standardı

## 1. Amaç

Web, Android ve izinli dış istemciler için tek, sürümlü, yetkilendirilmiş iş kapısı oluşturmak. API, database şemasının dışa açılmış hali değildir; use case sözleşmesidir.

## 2. Temel kurallar

| ID | Kural |
|---|---|
| API-001 | Base path `/api/v1`; major breaking change yeni path ister |
| API-002 | OpenAPI 3.1 source of truth; CI lint ve breaking-change kontrolü |
| API-003 | Her endpoint permission + scope + condition uygular |
| API-004 | Para/miktar/oran JSON'da string decimal |
| API-005 | Yazma işlemi `Idempotency-Key`; update `If-Match` |
| API-006 | Hata RFC 9457 Problem Details + stabil business code |
| API-007 | Liste cursor pagination; unbounded response yok |
| API-008 | Tarih ISO 8601; timestamp UTC `Z`; yasal tarih `YYYY-MM-DD` |
| API-009 | API, internal exception/SQL/table adı sızdırmaz |
| API-010 | İstemci SDK'ları OpenAPI'den üretilir |

## 3. Kimlik doğrulama profilleri

### Web session

- Same-origin secure cookie.
- State-changing request anti-CSRF tokenı.
- API response cookie veya token değeri döndürmez.
- Session revoke ve timeout merkezi.

### Android bearer

- OIDC Authorization Code + PKCE ile alınan access token.
- `aud`, `iss`, `exp`, `nbf`, signature ve authorized party kontrolü.
- Refresh API uygulamanın kendi endpoint'i değildir; OIDC sağlayıcı akışı.

### Service account

- Ayrı client/audience/scope.
- İnsan rollerini service token'a atama.
- IP/mTLS değerlendirmesi; rate limit ve rotation.

## 4. Request context

Header/claim kaynakları:

- `Authorization` veya web session.
- `X-Correlation-Id`: İstemci verebilir; biçim doğrulanır, yoksa server üretir.
- `Idempotency-Key`: Yazma komutunda UUID veya yüksek entropili opaque değer.
- `If-Match`: Aggregate version.
- `Accept-Language`: `tr-CY`, fallback `tr`.
- Company context claim + kullanıcının seçili şirketi. İstemcinin gönderdiği company ID tek başına güvenilmez.

`tenant_id` doğrudan kullanıcı header'ından alınmaz.

## 5. Kaynak adlandırma

```text
GET    /api/v1/parties
POST   /api/v1/parties
GET    /api/v1/parties/{partyId}
PATCH  /api/v1/parties/{partyId}
GET    /api/v1/parties/{partyId}/statement
POST   /api/v1/sales-orders/{orderId}/submit
POST   /api/v1/invoices/{invoiceId}/post
POST   /api/v1/invoices/{invoiceId}/reverse
```

İş eylemleri state değişimini açık fiille ifade eder. `POST /updateStatus` gibi genel endpoint yoktur.

## 6. Command response

Başarılı create:

```json
{
  "data": {
    "id": "019...",
    "status": "draft",
    "version": 1
  },
  "meta": {
    "traceId": "..."
  }
}
```

- HTTP `201` + `Location`.
- Tekrar aynı idempotency key/payload aynı status/body semantiği.
- Aynı key farklı payload `409 IDEMPOTENCY_KEY_REUSED`.
- Asenkron işlem `202` + job resource URI.

## 7. Problem Details

```json
{
  "type": "https://docs.example/errors/period-closed",
  "title": "Dönem kapalı",
  "status": 409,
  "code": "ACCOUNTING_PERIOD_CLOSED",
  "detail": "İşlem tarihi kapalı bir mali döneme aittir.",
  "traceId": "...",
  "errors": {
    "legalDate": ["Açık bir dönem seçin."]
  }
}
```

Kurallar:

- `detail` kullanıcı güvenli; teknik stack yok.
- `code` dil bağımsız ve değişmez.
- Validation `422`; auth yok `401`; yetki yok `403`; başka scope'taki resource için sızıntıyı azaltmak üzere çoğu durumda `404`.
- Concurrency `412 PRECONDITION_FAILED`; iş çatışması `409`.
- Rate limit `429` + `Retry-After`.

## 8. Liste, filtre ve arama

```text
GET /api/v1/invoices?status=posted&partyId=...&legalDateFrom=...&cursor=...&limit=50&sort=-legalDate
```

- Default 25, maksimum 100; export ayrı job.
- Cursor opaque ve imzalı/encode edilmiş stabil sıralama anahtarı.
- Filtre/sort allowlist; raw SQL/order string yok.
- Query timeout ve complexity sınırı.
- Response `nextCursor`, `hasMore`; exact total yalnız ucuzsa veya kullanıcı açık isterse.

## 9. Concurrency

- GET response `ETag: "<version>"`.
- PATCH/submit/post gibi state transition `If-Match` zorunlu.
- Çatışmada server güncel version ve kullanıcıya güvenli fark özeti verir.
- Posted resource patch endpoint'i yoktur.

## 10. İdempotency kayıt modeli

- Scope: tenant + actor/client + endpoint/command + key.
- Request method/path ve canonical body hash'i.
- Durum: in-progress/completed/failed-retryable/expired.
- Response status/body ref ve oluşturulan aggregate ID.
- Finansal command retention'ı normal API cache'den daha uzun; politika modülde tanımlanır.
- Eşzamanlı aynı key'de tek işlem kazanır, diğeri bekler veya deterministik çatışma alır.

PostgreSQL `platform.idempotency_record` uygulaması tenant, company, actor, command ve key birleşimini unique tutar. Aynı payload için completed HTTP status/body ve aggregate kimliği canonical JSON olarak replay edilir; farklı request hash stabil `IDEMPOTENCY_KEY_REUSED` çatışmasıdır. Runtime rolü kimlik/request alanlarını güncelleyemez ve yalnız `in-progress → completed` geçişinin kolonlarına sahiptir; trigger ikinci veya geri yönlü geçişi engeller. Kayıt silme/retention runtime API yetkisi değildir.

## 11. Büyük işler

- Import, export, rapor, e-Fatura batch ve maliyet yeniden hesaplama asenkron `job` olur.
- Job resource: state, progress, counts, warnings, errors, created/started/finished, result attachment.
- Kullanıcı yalnız kendi scope'undaki job'u görür.
- Retry/resume güvenli; cancel yalnız henüz kesinleşmemiş adımda.
- Dosya indirme kısa ömürlü same-origin endpoint veya imzalı URL; audit edilir.

## 12. Dosya yükleme

1. Upload session oluştur; beklenen tür/size/purpose.
2. Stream upload; temp/quarantine.
3. MIME magic, uzantı, boyut, hash, malware/policy kontrolü.
4. Domain kaynağına attach; tenant/company sahipliği.
5. Hatalı dosya silinir veya karantina retention'ı; kullanıcıya güvenli hata.

Filename path olarak kullanılmaz. HTML/SVG ve macro dosyaları risk sınıfına göre dönüştürülür veya bloklanır.

## 13. Webhook ve dış API

- Event envelope: `eventId`, `eventType`, `eventVersion`, `occurredAt`, `tenantRef`, `data`.
- HMAC veya asymmetric signature, timestamp ve replay window.
- At-least-once teslim; alıcı idempotent olmalı.
- Secret rotasyonu için iki aktif anahtar penceresi.
- PII minimum; payload yerine resource reference tercih edilir.
- Retry exponential backoff + jitter; dead-letter insan kuyruğu.

## 14. Versioning ve deprecation

- Additive alan değişikliği aynı major içinde; istemci bilinmeyen alanı tolere eder.
- Alan kaldırma/anlam değiştirme yeni major.
- Deprecated alan OpenAPI işareti, telemetry kullanım ölçümü ve en az iki release geçişi.
- Event schema kendi version'ına sahiptir; API version'ıyla zorunlu aynı değildir.
- Mobile eski sürümler için minimum supported version ve zorunlu update politikası.

## 15. Rate limiting

Farklı bucket'lar:

- Login/auth: Keycloak brute-force policy.
- Normal kullanıcı query.
- Ağır rapor/export.
- Service client.
- Upload.

Tenant bazlı adil kullanım ve endpoint maliyeti dikkate alınır. Limit güvenlik kontrolüdür, kapasite planının yerine geçmez.

## 16. OpenAPI ve istemci üretimi

- CI OpenAPI'yı deterministik üretir ve diff kontrol eder.
- .NET API build'i onaylı OpenAPI 3.1 kaynağını `docs/openapi/KaguERP.Api.json` dosyasına üretir;
  runtime doküman rotası yalnız Development ortamında açılır.
- TS/Kotlin istemci yalnız onaylı spec'ten `pnpm run generate:clients` ile üretilir. Üretim aracı
  OpenAPI Generator `7.24.0`, npm launcher `2.40.1` sürümüne sabitlenmiştir; script önce aracın
  erişilebilirliğini doğrular, ardından yalnız iki tanımlı generated dizini temizler.
- TypeScript çıktı `packages/api-client-ts`, Kotlin çıktı `apps/android/generated/api-client`
  altındadır. Web adaptörü same-origin cookie/BFF için boş base path ve `same-origin` credentials
  kullanır; Android adaptörü açıkça verilen HTTPS base URL ve güvenli katmandan enjekte edilen token
  provider ister. Jeneratörün `http://localhost` varsayılanı ürün kodunda otorite değildir.
- Generated modeller domain/UI model değildir; mapping katmanı vardır.
- Auth, retry, correlation, locale, decimal ve error mapping ortak client katmanında.
- Contract testleri real API ile generated client'ı çalıştırır.

## 17. API kabul kriterleri

- [ ] OpenAPI lint ve breaking-change gate.
- [ ] Permission/scope pozitif ve negatif test.
- [ ] Aynı idempotency key paralel 20 istekte tek sonuç.
- [ ] Stale ETag update'i reddediliyor.
- [ ] Problem Details içinde PII/stack yok.
- [ ] 10k+ satır listede cursor stabil; duplicate/missing satır yok.
- [ ] Web CSRF ve Android token audience negatif testleri geçiyor.
- [ ] Generated TS/Kotlin client smoke testleri geçiyor.

## 14. Business command ve çoklu durum sözleşmesi

ERP API’si tablo CRUD yüzeyi değildir. Kesinleştirme, reversal, allocation, reconciliation, reopen ve repost açık command endpoint’leridir. Bir belgenin yanıtı gerektiğinde şu ayrı durumları taşır:

- businessStatus: draft/approved/fulfilled/cancelled gibi süreç durumu;
- accountingStatus: not_required/pending/posted/exception/reversed;
- settlementStatus: unallocated/partial/allocated;
- bankStatus: unconfirmed/in_transit/reconciled/returned;
- integrationStatus: not_required/queued/delivered/rejected.

İstemci tek “status” veya “paid” alanından bu anlamları türetemez.

## 15. Tarih ve lineage alanları

Mali kaynak/ledger DTO’larında amaca göre documentDate, effectiveDate, recordedAt, postedAt, bookingDate ve valueDate adları kullanılır; belirsiz date alanı yasaktır. Read model yanıtı:

- asOf ve generatedAt;
- dataThrough/ledger watermark;
- projectionGeneration;
- sourceType/sourceId/sourceLineId;
- journalEntryId ve drillDownLinks;
- ruleVersion ve currency/rounding context

taşır. Rapor export manifesti aynı filtre, timezone, as-of, generation, sort ve yetki kapsamını saklar.

## 16. Finansal komut örüntüleri

- Payment create/post, paranın kaydını oluşturur; invoice allocation otomatik olacaksa açık policy ve sonuç listesi döner.
- Allocation create ve unallocate ayrı idempotent command’dır; payment veya journal silmez.
- Bank reconciliation önerisi read/preview; approve command maker-checker ve statement version ister.
- Repost önce dry-run impact endpoint’i üretir; execute ayrı permission, approval ID ve expected source checksum ister.
- Geçmiş tarihli posting 409/422 ile etki planı veya kilit nedeni döner; generic 500 olmaz.
- Uzun rapor/import/repost 202 + job resource döner; job sonucu checksum, satır sayısı ve reconciliation summary içerir.

Bulk komutlar “hepsi başarılı” varsaymaz. Atomic batch açık seçilmedikçe her satır için accepted/rejected, hata kodu ve idempotency sonucu döner; finansal kısmi başarı batch özeti ve yeniden çalıştırma anahtarıyla görünürdür.

## 17. Decimal ve para şeması

OpenAPI’de para, miktar, kur ve oran string tabanlı decimal formatıyla, scale/range örnekleriyle tanımlanır. amount tek başına gönderilmez; currency veya açık parent currency context’i gerekir. Debit/credit yönü signed amount ile karışık kullanılmaz; endpoint sözleşmesi bir yöntemi seçer ve property testle doğrular.
