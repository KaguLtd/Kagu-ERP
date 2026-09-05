# MP-04 Sales Order Lifecycle Foundation

- **Amaç:** Satış siparişini stok, gelir veya vergi olayı saymadan; sürümlü, denetlenebilir ve maker-checker sınırına hazır bir taahhüt yaşam döngüsü kurmak.
- **Master fazı ve kapısı:** MP-04 / siparişten iadeye miktar korunumu zincirinin sipariş başlangıcı.
- **Risk sınıfı:** R4 — ticari taahhüt, yetki/onay ve sonraki stok-cari-GL bağlantıları.
- **Durum:** in-progress.
- **Sahip:** Ürün/muhasebe sahipleri `atanmadı`; teknik uygulama Codex.
- **Başlangıç:** 4 Eylül 2026.
- **İlgili requirement ID'leri:** `SALES-ORD-001`, `SALES-ORD-002`, `SALES-ORD-002A`, `SALES-ORD-003`, `SALES-ORD-004`, `SALES-API-001`, `SALES-RES-001`, `SALES-FUL-001`, `WFL-INV-002`, `WFL-INV-003`, `DATA-002`.
- **Etkilenen modüller:** Sales Domain/Application/Infrastructure, API/Bootstrap, PostgreSQL migration ve kalite harness'leri.
- **Okunan belgeler:** `MASTER_PLAN.md`, `docs/README.md`, repository yapısı, ortak iş akışları, stok ve satış modül sözleşmeleri.
- **Definition of Ready sonucu:** Koşullu hazır. Authoritative item/base-UOM/miktar satırları kapsamda; fiyat, vergi, kredi, rezervasyon ve gelir tanıma kararları bu dilime alınmadı. Commitment lifecycle geri döndürülebilir ve politika seçmeyen bir temeldir.

## Kapsam

### Dahil

- `draft → submitted → approved → confirmed → partially_fulfilled → fulfilled → closed` ana yolu.
- Rejected, revise, withdraw ve fulfillment başlamadan cancel geçişleri.
- Exact optimistic expected version ve her geçişte tek sürüm artışı.
- Append-only transition event için previous/new state+version, actor, UTC occurrence, correlation ve gerekçe.
- Submitted version hazırlayanının kendi kaydını approve/reject edememesi.
- PostgreSQL `numeric(20,6)` uyumlu pozitif sipariş miktarı, typed base-UOM ve unique order-line→dispatch-line allocation evidence'ı.
- Taslakla aynı transaction'da oluşturulan 1–500 immutable authoritative sipariş satırı; item-company
  activation ve base-UOM FK doğrulaması, exact create replay ve decimal-string HTTP sözleşmesi.
- Partial/full lifecycle geçişinin aynı scope'taki allocation toplamlarından türetilmesi ve ordered quantity aşımının reddi.
- Permission + company scope doğrulaması, transaction-local RLS context ve caller-owned transaction.
- Current projection ile append-only transition event'ın deferred DB guard ile atomik eşleşmesi.
- Correlation tabanlı exact idempotent replay ve farklı immutable içerikte typed conflict.
- Permission/company scope kontrollü current state + tam transition timeline okuması.
- `/api/v1/sales-orders` create/detail ve allowlist lifecycle command uçları; UUID idempotency,
  quoted `If-Match`, ETag, güvenli Problem Details ve transaction içi authorization audit.
- Build sırasında deterministik üretilen OpenAPI 3.1 sözleşmesi ve Sales header/response drift kapısı.
- OpenAPI'den üretilen strict TypeScript ve Kotlin Sales istemcileri; web same-origin ve Android
  explicit HTTPS/token-provider adaptör sınırı.
- Confirmed exact version için Sales-owned published reservation-demand contract ve caller
  transaction'ına katılan scoped PostgreSQL source adapter'ı.

### Dahil değil

- Sipariş satırı revizyon/version akışı ile fiyat, iskonto, vergi, kur, adres veya ödeme koşulu snapshot'ları.
- Gerçek IAM permission grant yönetimi, approval workflow evidence veya outbox.
- HTTP runtime/E2E kanıtı.
- Rezervasyon, sevk, fatura, cari, stok, GL veya e-Fatura ekonomik olayı.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Sales Domain proje sınırı | Solution discovery ve architecture build | completed |
| 2 | Versioned order lifecycle | Ana yol, illegal transition, stale version ve UTC context | validating |
| 3 | Maker-checker ve reason sınırı | Self-approval/reject ile cancel/revise gerekçe negatifleri | validating |
| 4 | Quantity fulfilment evidence | Base-UOM line, unique dispatch allocation, over-fulfilment ve partial/full kanıtı | validating |
| 5 | Yetkili PostgreSQL lifecycle persistence | Forced RLS, exact version, append-only event, maker-checker DB guard | validating |
| 6 | Idempotent retry ve negatif scope | Exact correlation replay/conflict, cross-company görünmezlik, DELETE yasağı | validating |
| 7 | Yetkili lifecycle detail query | Current state + version sıralı timeline, tutarlılık ve RLS negatifleri | validating |
| 8 | Sales order HTTP contract | Create/detail/allowlist transitions, UUID retry, ETag/If-Match, Problem Details, atomik audit | validating |
| 9 | OpenAPI 3.1 contract | Build-time spec, required concurrency/idempotency headers, response matrix ve drift harness | validating |
| 10 | Generated TS/Kotlin clients | Sabit generator, güvenli clean/regenerate, typed Sales API ve platform adaptör compile | validating |
| 11 | Authoritative order-line commitment | Atomik header+line create, item/UOM FK, exact replay, append-only privilege ve client DTO drift | validating |
| 12 | Published reservation demand | Confirm permission/scope, confirmed exact version, immutable line snapshot ve stale/unconfirmed negatifleri | validating |

## Riskler ve sınırlar

- Lifecycle kararı tek başına permission veya approval değildir; Application katmanı authoritative evidence olmadan geçişi kalıcılaştıramaz.
- `confirmed` olmak stok rezervasyonu veya stok hareketi üretmez; bu bağlantı sonraki atomik orchestration dilimidir.
- `fulfilled` yalnız gelecekte authoritative fulfilment allocation toplamından türetilecektir; bu ilk dilim caller beyanını production otoritesi saymaz.

## İlerleme günlüğü

- 4 Eylül 2026: Sales Domain solution'a eklendi. Sürüm kontrollü lifecycle, append-only event contract, reason ve maker-checker sınırları ile `SALES-ORD-001` dar kontrolü yazıldı. Sales Domain, Unit ve Architecture Release build'leri `0 warning/error`; runtime kullanıcıyla kararlaştırılan MP-04 toplu kapısına bırakıldı.
- 4 Eylül 2026: Lifecycle partial/full geçişleri caller durum beyanından çıkarılıp `SALES-FUL-001` quantity evidence'ına bağlandı. Pozitif `numeric(20,6)` uyumlu line quantity, canonical base-UOM, unique allocation ve exact tenant/company/order/line/dispatch scope'u zorunlu; allocation toplamı ordered quantity'yi aşamaz. Partial evidence full geçişi, eksik evidence fulfilment geçişini reddeder. Sales Domain ve Unit Release build'leri `0 warning/error`; runtime MP-04 toplu kapısına ertelendi.
- 4 Eylül 2026: `0044` migration'ı forced-RLS current order projection ve UPDATE/DELETE yetkisi olmayan append-only transition history kurdu. DB guard exact `+1` version, legal status/transition çifti, maker-checker, zorunlu gerekçe ve event↔projection atomikliğini deferred doğrular. Application permission command'ları ve caller transaction'ına katılan PostgreSQL writer; exact correlation replay, farklı içerik conflict ve concurrent retry sonrası yeniden okuma davranışıyla eklendi. Cross-company RLS, create/transition replay ve DELETE privilege negatiflerini içeren gerçek PostgreSQL senaryosu MP-04 toplu runtime kapısı için hazırlandı. Sales Application, Infrastructure, Unit, Integration, Architecture ve Migrator Release derlemeleri `0 warning/error`; runtime/migration uygulaması plana uygun ertelendi.
- 4 Eylül 2026: `SALES-ORD-004` altında `sales.order.view` permission/company scope command'ı ve caller transaction'ında header'ı `FOR SHARE` kilitleyerek version sıralı transition geçmişini okuyan PostgreSQL loader eklendi. Application view contract'ı eksik/sıra dışı event zincirini ve current state uyumsuzluğunu fail-closed reddeder. Tam üç-event submit→approve→confirm rekonstrüksiyonu ile başka-company not-found negatifi gerçek DB senaryosuna eklendi. İlk Unit analizör koşusundaki kullanılmayan negatif fixture `CA1806` hatası explicit discard ile düzeltildi; Sales Infrastructure, Unit, Integration ve Architecture Release derlemeleri `0 warning/error`. Runtime MP-04 toplu kapısına bırakıldı.
- 4 Eylül 2026: `SALES-API-001` create/detail ve fulfilment hariç allowlist lifecycle action uçlarıyla uygulandı. UUID `Idempotency-Key` create için aggregate, transition için correlation kimliği; quoted positive `If-Match` optimistic version kaynağıdır. ETag/Location, lower-snake status, ordered timeline ve 404/409/412/422/503 Problem Details eşlemesi eklendi. API yalnız Application gateway'ine bağlıdır; PostgreSQL gateway lifecycle ve allowed authorization audit kaydını aynı transaction'da commit eder, bağlantı yoksa fail-closed unavailable implementation çalışır. Contract harness route/header/action/DTO sınırlarını kapsar. Restore ilk sandbox koşusunda NuGet ağ kısıtıyla başarısız, izinli tekrarında başarılı oldu; API, Integration, Unit ve Architecture Release derlemeleri `0 warning/error`. OpenAPI üretimi ve HTTP/runtime kanıtı MP-04 toplu kapısına açık borçtur.
- 4 Eylül 2026: Birinci taraf `Microsoft.AspNetCore.OpenApi` ve build-time `Microsoft.Extensions.ApiDescription.Server` 10.0.11 paketleri merkezi ve kilitli eklendi; ikisi de .NET 10 patch hattında Microsoft tarafından bakımı yapılan MIT bileşenleridir ve üçüncü taraf UI/runtime yüzeyi eklemez. Build, `docs/openapi/KaguERP.Api.json` altında OpenAPI 3.1.1 üretir. Sales operationId'leri, bearer auth gereksinimi, request şemaları, transition action enum'u, zorunlu UUID idempotency/quoted version header'ları, success/Problem Details cevap matrisi ve ortak problem şeması belgelendi. Doküman üretim prosesi dış OIDC yapılandırmasını okumaz; normal API başlangıcındaki authority/audience fail-fast kontrolü korunur ve runtime spec rotası yalnız Development'ta açılır. Üretilen dosya için architecture drift kontrolü hazırlandı. API ve Architecture Release derlemeleri `0 warning/error`; contract harness runtime koşusu MP-04 toplu kapısına bırakıldı. TS/Kotlin üretimi ve HTTP runtime kanıtı açık borçtur.
- 4 Eylül 2026: Apache-2.0 lisanslı OpenAPI Generator `7.24.0` ve npm launcher `2.40.1` sabitlendi; Android transport için yine Apache-2.0 lisanslı Moshi `1.15.2` ve OkHttp `5.4.0` kullanıldı. Güvenli üretim scripti yalnız tanımlı TS/Kotlin çıktılarını temizler, workspace `node_modules` bağlantısını korur ve silmeden önce generator/JAR erişimini doğrular. Sales istemcileri UUID `Idempotency-Key`, quoted `If-Match`, action enum'u, response tipleri ve bearer gereksinimiyle üretildi; stale inline version modeli temiz üretimde kalmadı. Web adaptörü same-origin cookie/BFF sınırında token saklamadan çalışır. Android adaptörü explicit HTTPS base URL ister, yalnız emulator loopback için HTTP'ye izin verir ve token kaynağını güvenli auth katmanından enjekte eder. TypeScript client build, web strict typecheck, Android `compileDebugKotlin` ve Architecture Release build'i geçti; runtime client/API smoke MP-04 birleşik kapanışına bırakıldı. OpenAPI Generator'ın 3.1 desteği upstream tarafından beta olarak işaretlendiği için generated drift harness korunacaktır.
- 5 Eylül 2026: `SALES-ORD-002A` ile taslak siparişin authoritative satır kaynağı kuruldu. Create komutu 1–500 unique satırı immutable commitment olarak taşır; `0045` migration'ı header+line atomikliğini deferred guard, item-company/base-UOM FK'leri, forced RLS ve runtime rolündeki SELECT/INSERT-only yetkiyle korur. Aynı aggregate idempotency kimliğinin farklı satırlarla tekrarı conflict'tir. Lifecycle detail ve transition cevapları persisted satırları döndürür; miktar HTTP ve generated TS/Kotlin istemcilerinde invariant decimal string kalır. Sales Infrastructure, API, Migrator, Unit, Architecture ve Integration Release derlemeleri `0 warning/error`; istemciler yeniden üretildi. Runtime/migration uygulaması ve contract çalıştırması MP-04 toplu kapısına bırakıldı.
- 5 Eylül 2026: `SALES-RES-001` için bağımsız `Sales.Contracts` katmanı ve transaction-bound PostgreSQL source adapter eklendi. Inventory veya composition katmanı Sales tablolarını doğrudan okumadan confirmed exact order version'a ait immutable order-line/item/base-UOM/decimal quantity snapshot'ını alabilir. Adapter company scope ve `sales.order.confirm` permission ister; approved fakat unconfirmed, stale version ve yetkisiz actor negatifleri MP-04 gerçek DB senaryosuna eklendi. Contracts, Sales Infrastructure, Integration ve Architecture Release derlemeleri `0 warning/error`; runtime MP-04 toplu kapısına bırakıldı.

## Tamamlanma kanıtı

- [ ] Domain runtime kabul kontrolleri.
- [x] Domain ve kalite harness derlemesi.
- [x] Mimari proje sınırı.
- [x] PostgreSQL persistence, forced RLS ve append-only event guard tasarımı.
- [x] MP-04 toplu kapısında çalıştırılacak gerçek DB senaryosu.
- [x] API route, ETag/If-Match, UUID idempotency ve Problem Details contract harness'i.
- [x] OpenAPI 3.1 build-time üretimi ve Sales drift kontrolü.
- [x] Generated TS/Kotlin client üretimi, drift kontrolü ve platform compile kanıtı.
- [x] Politika dışı ekonomik etkilerin kapsam dışında tutulması.
