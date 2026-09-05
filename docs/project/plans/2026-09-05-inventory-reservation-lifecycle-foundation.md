# MP-04 Inventory Reservation Lifecycle Foundation

- **Amaç:** Satış veya başka bir talep satırına ayrılan stok miktarını, stok hareketiyle karıştırmadan sürümlü ve append-only geçişlere hazır bir Inventory sözleşmesi olarak kurmak.
- **Master fazı ve kapısı:** MP-04 / eşzamanlı reservation ve miktar korunumu.
- **Risk sınıfı:** R4 — stok kullanılabilirliği, depo kapsamı ve satış-sevk zinciri.
- **Durum:** in-progress.
- **Sahip:** Ürün/muhasebe sahipleri `atanmadı`; teknik uygulama Codex.
- **Başlangıç:** 5 Eylül 2026.
- **İlgili requirement ID'leri:** `INV-RES-001`, `INV-RES-002`, `INV-RES-003`, `INV-AUTH-001`, `SALES-ORD-002A`, `SALES-RES-001`, `SALES-FUL-001`.
- **Okunan belgeler:** `MASTER_PLAN.md`, `docs/modules/04-items-inventory.md`, `docs/modules/05-sales.md`, `docs/00-foundation/07-cross-cutting-workflows.md`.
- **Definition of Ready sonucu:** Koşullu hazır. Demand source, miktar ve lifecycle politika seçmeden modellenebilir. Depo seçim zamanı, expiry süresi ve satış confirm orchestration politikası seçilmediği için bu ilk dilim persistence veya available sonucu üretmez.

## Kapsam

### Dahil

- Versioned demand source type/id/line/version kimliği.
- Exact tenant/company/item/warehouse/base-UOM ve pozitif `numeric(20,6)` rezerv miktarı.
- `active → partially_consumed → consumed | released | expired` lifecycle.
- Append-only event için previous/new version+status, exact consume miktarı, actor, correlation ve PostgreSQL-safe UTC occurrence.
- Remaining miktarının yalnız state ve exact decimal değerlerden türetilmesi.
- Over-consumption, terminal state mutation, gerekçesiz release ve erken expiry fail-closed negatifleri.
- `inventory.reservation.create`, company scope ve actor-bound authoritative warehouse evidence isteyen
  authorization candidate; farklı actor/company/depo evidence'ı fail-closed kalır.
- Üretici modülün published contract adaptöründen yüklenecek exact source version, item, base-UOM ve
  azami reservable quantity demand evidence'ı; stale version ve demand üstü miktar fail-closed kalır.
- Sales-owned confirmed-order snapshot'ını, dönen scope ve version bağlamını yeniden doğrulayarak
  Inventory-owned reservation demand evidence'a çeviren modüller arası adaptör.

### Dahil değil

- Available sorgusu, eşzamanlı DB kilidi, over-reservation policy veya persisted reservation.
- Satış confirm ile atomik orchestration, depo seçim politikası, lot/seri, sevk tüketimi veya stok hareketi.
- API/web/Android.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Reservation lifecycle domain | Demand lineage, exact quantity, partial/full/release/expiry negatifleri | validating |
| 2 | Authorized warehouse-scoped candidate | Permission + authoritative warehouse evidence | validating |
| 3 | Sales → Inventory demand adapter | Published contract, exact scope/version ve kayıpsız line mapping | validating |
| 4 | Atomic available reservation persistence | PostgreSQL concurrency, forced RLS, idempotency, over-reservation block | blocked-policy |
| 5 | Sales confirm orchestration | Order version + line snapshot + reservation aynı transaction | blocked-policy |
| 6 | Dispatch consumption/release | Partial consume, cancel/reject/expiry release event'leri | planned |

## Açık politika sınırları

- Depo rezervasyonda mı seçilecek, yoksa confirm anında şirket politikasıyla otomatik mi çözülecek?
- Expiry kullanılacaksa süre ve uzatma yetkisi nedir?
- Reservation ile eşzamanlı stok issue/transfer aynı position lock protokolüne nasıl bağlanacak?

Bu sorular ilk domain lifecycle'ı bloklamaz; authoritative persistence ve satış confirm orchestration başlamadan yanıtlanmalıdır.

## İlerleme günlüğü

- 5 Eylül 2026: Inventory-owned reservation state, versioned demand lineage, warehouse/base-UOM hedefi ve exact decimal consume/release/expiry geçişleri eklendi. Persistence ve available sonucu üretilmedi; bu nedenle eşzamanlılık veya over-reservation kanıtı henüz ileri sürülmüyor. Domain ve Unit Release derlemesi dar kapıdır; runtime MP-04 sonunda çalıştırılacaktır.
- 5 Eylül 2026: `INV-RES-002` authorization candidate eklendi. `inventory.reservation.create`, exact company scope ve transaction içinde authoritative olarak yüklenecek actor-bound warehouse evidence olmadan persistence adayı üretilemez. Candidate demand source'un gerçekten var olduğunu iddia etmez; bu doğrulama ileride Inventory orchestration'ın çağıracağı published module contract üzerinden yapılacaktır. Inventory Application ve Unit Release derlemeleri dar kapıdır; runtime MP-04 sonunda çalıştırılacaktır.
- 5 Eylül 2026: Candidate exact demand evidence ile sıkılaştırıldı. Evidence source type/id/line/version, item, base-UOM ve azami ayrılabilir miktarı taşır; stale source version, farklı ürün/birim veya talebi aşan reservation reddedilir. Evidence tipinin varlığı tek başına otorite değildir; persistence öncesi producer module published contract adaptöründen aynı transaction bağlamında yüklenmesi zorunlu kalır. Inventory Application ve Unit Release derlemeleri `0 warning/error`.
- 5 Eylül 2026: İlk producer contract olarak `Sales.Contracts` reservation-demand snapshot'ı ve Sales-owned PostgreSQL source eklendi. Yalnız confirmed exact order version ve `sales.order.confirm` permission/company scope altında satır yayımlar; Inventory Sales tablosu okumaz.
- 5 Eylül 2026: `INV-RES-003` Sales → Inventory demand adaptörü eklendi. Adaptör producer'ın döndürdüğü tenant/company/order/version bağlamını request ile birebir doğrulayıp her satırı Inventory quantity/UOM değer nesneleri üzerinden reservation evidence'a çevirir; null veya scope/version sapması fail-closed kalır. Exact order-line bulunmadan candidate kurulamaz; bulunan satırdan üretilen state ayrıca permission, company, actor-bound warehouse ve demand-capacity invariant'larından geçer. Inventory Infrastructure, Integration ve Architecture Release derlemeleri `0 warning/error`; runtime MP-04 toplu kapısına bırakıldı. Candidate builder persistence/available sonucu üretmez ve confirm orchestration hâlâ kapsam dışıdır.
