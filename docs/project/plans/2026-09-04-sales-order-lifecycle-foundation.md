# MP-04 Sales Order Lifecycle Foundation

- **Amaç:** Satış siparişini stok, gelir veya vergi olayı saymadan; sürümlü, denetlenebilir ve maker-checker sınırına hazır bir taahhüt yaşam döngüsü kurmak.
- **Master fazı ve kapısı:** MP-04 / siparişten iadeye miktar korunumu zincirinin sipariş başlangıcı.
- **Risk sınıfı:** R4 — ticari taahhüt, yetki/onay ve sonraki stok-cari-GL bağlantıları.
- **Durum:** in-progress.
- **Sahip:** Ürün/muhasebe sahipleri `atanmadı`; teknik uygulama Codex.
- **Başlangıç:** 4 Eylül 2026.
- **İlgili requirement ID'leri:** `SALES-ORD-001`, `SALES-ORD-002`, `SALES-ORD-003`, `SALES-ORD-004`, `SALES-FUL-001`, `WFL-INV-002`, `WFL-INV-003`, `DATA-002`.
- **Etkilenen modüller:** Sales Domain/Application/Infrastructure, PostgreSQL migration ve kalite harness'leri.
- **Okunan belgeler:** `MASTER_PLAN.md`, `docs/README.md`, repository yapısı, ortak iş akışları, stok ve satış modül sözleşmeleri.
- **Definition of Ready sonucu:** Koşullu hazır. Fiyat, vergi, kredi, rezervasyon ve gelir tanıma kararları bu dilime alınmadı; salt commitment lifecycle geri döndürülebilir ve politika seçmeyen bir temeldir.

## Kapsam

### Dahil

- `draft → submitted → approved → confirmed → partially_fulfilled → fulfilled → closed` ana yolu.
- Rejected, revise, withdraw ve fulfillment başlamadan cancel geçişleri.
- Exact optimistic expected version ve her geçişte tek sürüm artışı.
- Append-only transition event için previous/new state+version, actor, UTC occurrence, correlation ve gerekçe.
- Submitted version hazırlayanının kendi kaydını approve/reject edememesi.
- PostgreSQL `numeric(20,6)` uyumlu pozitif sipariş miktarı, typed base-UOM ve unique order-line→dispatch-line allocation evidence'ı.
- Partial/full lifecycle geçişinin aynı scope'taki allocation toplamlarından türetilmesi ve ordered quantity aşımının reddi.
- Permission + company scope doğrulaması, transaction-local RLS context ve caller-owned transaction.
- Current projection ile append-only transition event'ın deferred DB guard ile atomik eşleşmesi.
- Correlation tabanlı exact idempotent replay ve farklı immutable içerikte typed conflict.
- Permission/company scope kontrollü current state + tam transition timeline okuması.

### Dahil değil

- Sipariş satırı, fiyat, iskonto, vergi, kur, adres veya ödeme koşulu snapshot'ları.
- Gerçek IAM permission grant yönetimi, approval workflow evidence, API, merkezi audit writer veya outbox.
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

## Riskler ve sınırlar

- Lifecycle kararı tek başına permission veya approval değildir; Application katmanı authoritative evidence olmadan geçişi kalıcılaştıramaz.
- `confirmed` olmak stok rezervasyonu veya stok hareketi üretmez; bu bağlantı sonraki atomik orchestration dilimidir.
- `fulfilled` yalnız gelecekte authoritative fulfilment allocation toplamından türetilecektir; bu ilk dilim caller beyanını production otoritesi saymaz.

## İlerleme günlüğü

- 4 Eylül 2026: Sales Domain solution'a eklendi. Sürüm kontrollü lifecycle, append-only event contract, reason ve maker-checker sınırları ile `SALES-ORD-001` dar kontrolü yazıldı. Sales Domain, Unit ve Architecture Release build'leri `0 warning/error`; runtime kullanıcıyla kararlaştırılan MP-04 toplu kapısına bırakıldı.
- 4 Eylül 2026: Lifecycle partial/full geçişleri caller durum beyanından çıkarılıp `SALES-FUL-001` quantity evidence'ına bağlandı. Pozitif `numeric(20,6)` uyumlu line quantity, canonical base-UOM, unique allocation ve exact tenant/company/order/line/dispatch scope'u zorunlu; allocation toplamı ordered quantity'yi aşamaz. Partial evidence full geçişi, eksik evidence fulfilment geçişini reddeder. Sales Domain ve Unit Release build'leri `0 warning/error`; runtime MP-04 toplu kapısına ertelendi.
- 4 Eylül 2026: `0044` migration'ı forced-RLS current order projection ve UPDATE/DELETE yetkisi olmayan append-only transition history kurdu. DB guard exact `+1` version, legal status/transition çifti, maker-checker, zorunlu gerekçe ve event↔projection atomikliğini deferred doğrular. Application permission command'ları ve caller transaction'ına katılan PostgreSQL writer; exact correlation replay, farklı içerik conflict ve concurrent retry sonrası yeniden okuma davranışıyla eklendi. Cross-company RLS, create/transition replay ve DELETE privilege negatiflerini içeren gerçek PostgreSQL senaryosu MP-04 toplu runtime kapısı için hazırlandı. Sales Application, Infrastructure, Unit, Integration, Architecture ve Migrator Release derlemeleri `0 warning/error`; runtime/migration uygulaması plana uygun ertelendi.
- 4 Eylül 2026: `SALES-ORD-004` altında `sales.order.view` permission/company scope command'ı ve caller transaction'ında header'ı `FOR SHARE` kilitleyerek version sıralı transition geçmişini okuyan PostgreSQL loader eklendi. Application view contract'ı eksik/sıra dışı event zincirini ve current state uyumsuzluğunu fail-closed reddeder. Tam üç-event submit→approve→confirm rekonstrüksiyonu ile başka-company not-found negatifi gerçek DB senaryosuna eklendi. İlk Unit analizör koşusundaki kullanılmayan negatif fixture `CA1806` hatası explicit discard ile düzeltildi; Sales Infrastructure, Unit, Integration ve Architecture Release derlemeleri `0 warning/error`. Runtime MP-04 toplu kapısına bırakıldı.

## Tamamlanma kanıtı

- [ ] Domain runtime kabul kontrolleri.
- [x] Domain ve kalite harness derlemesi.
- [x] Mimari proje sınırı.
- [x] PostgreSQL persistence, forced RLS ve append-only event guard tasarımı.
- [x] MP-04 toplu kapısında çalıştırılacak gerçek DB senaryosu.
- [x] Politika dışı ekonomik etkilerin kapsam dışında tutulması.
