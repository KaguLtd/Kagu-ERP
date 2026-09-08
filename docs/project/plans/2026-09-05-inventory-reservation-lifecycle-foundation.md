# MP-04 Inventory Reservation Lifecycle Foundation

- **Amaç:** Satış veya başka bir talep satırına ayrılan stok miktarını, stok hareketiyle karıştırmadan sürümlü ve append-only geçişlere hazır bir Inventory sözleşmesi olarak kurmak.
- **Master fazı ve kapısı:** MP-04 / eşzamanlı reservation ve miktar korunumu.
- **Risk sınıfı:** R4 — stok kullanılabilirliği, depo kapsamı ve satış-sevk zinciri.
- **Durum:** in-progress.
- **Sahip:** Ürün/muhasebe sahipleri `atanmadı`; teknik uygulama Codex.
- **Başlangıç:** 5 Eylül 2026.
- **İlgili requirement ID'leri:** `INV-RES-001`, `INV-RES-002`, `INV-RES-003`, `INV-AUTH-001`, `SALES-ORD-002A`, `SALES-RES-001`, `SALES-FUL-001`.
- **Okunan belgeler:** `MASTER_PLAN.md`, `docs/modules/04-items-inventory.md`, `docs/modules/05-sales.md`, `docs/00-foundation/07-cross-cutting-workflows.md`.
- **Definition of Ready sonucu:** DEC-MP01-025 ile seçili depo, kısmi rezervasyon ve expiry olmaması kararı hazırdır. Persistence in-progress; request gate, demand/position kilitleri, güncel kapasite okuması ve atomik yazma tamamlanmadan gerçek reservation veya available sonucu yayımlanmaz. Değerleme/backdate kararları ayrı kalır.

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
| 4 | Atomic available reservation persistence | PostgreSQL concurrency, forced RLS, idempotency, over-reservation block | in-progress |
| 5 | Sales confirm orchestration | Order version + line snapshot + reservation aynı transaction | planned |
| 6 | Dispatch consumption/release | Partial consume, cancel/reject/expiry release event'leri | planned |

## Açık politika sınırları

Güncel durum: DEC-MP01-025 aşağıdaki depo/expiry sorularını çözdü. Depo kullanıcı tarafından
seçilir, kısmi rezervasyon yapılır, otomatik expiry yoktur. Aşağıdaki sorular tarihsel bağlamdır;
miktar rezervasyonunda artık kullanıcı cevabı beklenmiyor. Değerleme ve backdate ayrı açık kalır.

- Depo rezervasyonda mı seçilecek, yoksa confirm anında şirket politikasıyla otomatik mi çözülecek?
- Expiry kullanılacaksa süre ve uzatma yetkisi nedir?
- Reservation ile eşzamanlı stok issue/transfer ortak position lock protokolüne bağlanmalıdır; bu bir teknik uygulama bağımlılığıdır, kullanıcı ürün kararı değildir.

Bu sorular ilk domain lifecycle'ı bloklamaz; authoritative persistence ve satış confirm orchestration başlamadan yanıtlanmalıdır.

## İlerleme günlüğü

- 8 Eylül 2026 doğrulama/teslim: Architecture ve Integration `dotnet build ... -c Release --no-restore -v:q` derlemeleri 0 uyarı/0 hata; `git diff --check` temiz. SQL ve DB senaryoları çalıştırılmadı. Kullanıcıya genel kapsam için yaklaşık %30, MP-04 için yaklaşık ilk üçte bir şeklinde mühendislik tahmini verildi; bunlar ölçülmüş kabul/test oranları değildir. MP-00/02 completed, MP-03 validating, MP-04 in-progress durumu esas alınır.

- 8 Eylül 2026 lifecycle persistence: `0048` immutable consume/release event tablosu eklendi; exact ardışık version, terminal-state reddi, pozitif consume/zero release, cumulative consumed ve remaining korunumu DB trigger'ıyla tanımlandı. Company/actor/creation FK, forced RLS, correlation uniqueness ve runtime SELECT-only sınırı korunur. Balance loader effective/recorded kesitinde son olaydan aktif rezervasyonu, demand için consumed+remaining toplamını döndürür. Sentetik owner transaction senaryosu 10 rezervasyon → 6 consume → 4 release; aktif 0, demand taahhüdü 6 ve fazla/terminal tüketim retlerini kapsar. Migration expand-only; önceki binary'ye dönüşte tablo/veri korunur, down/delete yok. Runtime/migration uygulama MP-04 toplu kapısında. Bloke stok kanıtı ve atomik yazıcı bağlantısı hâlâ açık; bu tablolar gerçek sevk posting'i açmaz. Master kapısı değişmedi; commit/push yapılmadı.

- 8 Eylül 2026 balance doğrulaması: `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release --no-restore -v:q` 0 uyarı/0 hata; `git diff --check` temiz. PostgreSQL sorgusunun çalışma zamanı kanıtı toplu kapıda bekliyor.

- 8 Eylül 2026 balance dilimi: `PostgresInventoryReservationBalanceLoader` eklendi; exact scope/source/version ve azami demand miktarı doğrulanır. Request gate sonrasında version/depo bağımsız source-line lock ve position lock alınır; beklemeden sonra warehouse yetkisi yeniden doğrulanır. DB cutoff ile on-hand, position ve demand bazında brüt creation toplamları tek sorguda okunur. Zero-result request'in stok ayırmadığı ve boş toplamlar app-rolü DB senaryosuna eklendi. Runtime MP-04 sonuna ertelendi. Brüt creation toplamı active reserved değildir; tüketim/release ve blocked kaynağı olmadan bu snapshot'tan production available üretilmez. Sıradaki zorunlu adım reservation lifecycle persistence ve blocked quantity evidence; ardından atomik create writer açılabilir. INSERT yetkileri kapalı, yeni API/migration yok; master kapısı değişmedi. Commit/push yapılmadı.

- 8 Eylül 2026 request gate: `PostgresInventoryReservationRequestGate` tenant/company/request kimliğiyle transaction lock alır; fingerprint/depo değişimi farklı lock üretmez. ReadCommitted ve aynı connection/transaction zorunlu, authoritative warehouse yetkisi kilitten önce ve sonra denetlenir. Replay fresh statement snapshot ile okunur. İki bağlantılı gerçek DB senaryosu farklı miktarla lock timeout, owner commit sonrası app rolüyle zero replay ve conflict davranışını kapsar. Fixture owner yazımı yalnız test içindir; runtime INSERT hâlâ kapalıdır. Integration Release build 0 uyarı/0 hata; runtime MP-04 sonuna ertelendi. Sıradaki adım request kilidi sonrasında demand kilidi ve authoritative kapasite/yazma; null replay kapasite veya yazma izni değildir. Migration/API değişmedi; commit/push yapılmadı.

- 8 Eylül 2026 replay dilimi: `InventoryReservationRequest` versioned canonical JSON/SHA-256 fingerprint ve permission/company sınırıyla eklendi. `PostgresInventoryReservationReplayLoader` caller transaction'ında authoritative warehouse scope'u yenileyip immutable istek sonucunu okur; sıfır sonuç değişmeden döner, farklı içerik typed conflict'tir. Unit decimal normalization/effective-date farkı ve DB harness sıfır replay/değişmiş miktar conflict senaryoları eklendi. Bu DB fixture migrator transaction'ında çalışır; runtime rolü RLS izolasyon kanıtının yerine geçmez. Unit ve Integration Release derlemeleri 0 uyarı/0 hata; runtime MP-04 sonunda. Yeni migration yok. Writer ve API açılmadı; request kilidi altında ikinci kontrol ve kapasite hesap/yazımı sıradaki adımdır. Master kapısı değişmedi; commit/push yapılmadı.

- 8 Eylül 2026 sonuç: Architecture Release build (`dotnet build tests/Architecture/KaguERP.ArchitectureChecks.csproj -c Release --no-restore -v:q`) 0 uyarı/0 hata; `git diff --check` temiz. Yeni SQL PostgreSQL üzerinde henüz uygulanmadı; derleme runtime constraint/transaction doğrulaması değildir.

- 8 Eylül 2026 devam: `0047_inventory_reservation_request_result` migration'ı sıfır/kısmi sonuç ve fingerprint kaydını ekledi. Company/request PK, pozitif sonuç için exact creation bağlantısı, reciprocal deferred FK ve sıfır sonuç/pozitif creation çelişkisini reddeden çift trigger ile commit atomikliği tasarlandı. Her iki tablo runtime SELECT-only kalır. Sentetik DB senaryosu sıfır sonuç, duplicate key ve owner seviyesinde immutable UPDATE reddini kapsar; runtime MP-04 toplu kapısına bırakıldı. Expand migration eski binary ile uyumlu; geri dönüş veriyi koruyarak önceki binary'dir, down/delete yok. API veya stok yazımı açılmadı. Sıradaki adım canonical fingerprint/replay ve kilit altında capacity writer; şema varlığı bunların tamamlandığı anlamına gelmez. Master kapısı değişmedi; commit/push yapılmadı.

- 8 Eylül 2026 dar doğrulama: `dotnet build tests/Architecture/KaguERP.ArchitectureChecks.csproj -c Release --no-restore -v:q` 0 uyarı/0 hata; `git diff --check` temiz. SQL henüz PostgreSQL üzerinde uygulanıp çalıştırılmadı; build yalnız C# ve migration resource derlemesini kanıtlar.

- 8 Eylül 2026: `0046_inventory_reservation_creation` expand migration eklendi. Inventory-owned immutable create snapshot; tenant/company, request uniqueness, item-company/UOM/depo/actor FK'leri, pozitif ve talebi aşmayan numeric miktar, effective date ve recorded timestamp, correlation ve policy sürümü taşır. Force RLS uygulanır; runtime SELECT-only olduğundan capacity writer hazır olmadan gerçek reservation yazılamaz. UPDATE/DELETE trigger'ı owner seviyesinde de hatalı yerinde düzeltmeyi reddeder. Catalog/RLS/privilege/constraint kontrolü gerçek DB harness'ine eklendi; runtime ve migration uygulaması MP-04 toplu kapısına bırakıldı. Yeni tablo önceki migration'ları değiştirmez; eski binary tabloyu kullanmaz, geri dönüş eski binary + tabloyu koruma şeklindedir. Veri silen down migration yoktur. Şema tek başına available veya idempotent replay garantisi değildir; zero-result request kaydı, lifecycle ve atomik writer sonraki dilimde tamamlanacaktır. API/audit/GL akışı açılmadı. Master kapısı değişmedi; commit/push yapılmadı.

- 7 Eylül 2026 uygulama: Kullanıcının Devam yanıtı DEC-MP01-025 olarak kaydedildi. `INV-RES-004` kısmi kapasite hesabı ve `INV-LOCK-001` transaction advisory lock eklendi; immediate-transfer writer iki pozisyonu birlikte kilitler. ReadCommitted dışında çalışma reddedilir; lock bütün transaction boyunca tutulur. Unit miktar korunumu/kesirli değer ve iki PostgreSQL bağlantısında lock timeout + rollback sonrası tekrar edinme senaryosu yazıldı. Unit ve Integration Release build 0 uyarı/0 hata; runtime kullanıcı kararıyla MP-04 sonuna ertelendi. Yeni migration yok; kalıcı reservation writer henüz eklenmedi. Sonraki adım kilit altındaki güncel stok/rezervasyon okumasıyla atomik create ve idempotency persistence. Kilit tek başına negatif stok engeli değildir. Commit/push yapılmadı.

- 7 Eylül 2026: Persistence öncesi açık ürün kararları [rezervasyon önerisinde](2026-09-07-reservation-policy-proposal.md) somutlaştırıldı: depo seçimi, yetersiz stokta kısmi rezervasyon ve otomatik süre sonu. Öneriler henüz kabul edilmedi. Teknik kilit protokolü ürün kararı listesinden ayrıldı. Davranış değişmedi; test/derleme çalıştırılmadı. Sıradaki bağımlı adım kullanıcı cevabıyla karar kaydını güncelleyip persistence kapsamını açmaktır.

- 7 Eylül 2026 doğrulama: `dotnet build tests/Architecture/KaguERP.ArchitectureChecks.csproj -c Release --no-restore -v:q` başarılı, 0 uyarı/0 hata; `git diff --check` temiz. Runtime testleri çalıştırılmadı. Faz kapısı değişmedi; sıradaki adım sevk hazırlama sözleşmesinin mevcut sipariş/rezervasyon bağımlılıklarıyla modellenmesidir. Commit/push yapılmadı.

- 7 Eylül 2026: `INV-RES-002/003` R4 yetki sırası düzeltmesi seçildi. Candidate builder Sales demand kaynağını çağırmadan permission, tenant/company ve actor-bound depo kanıtını ortak `EnsureAccess` kontrolünden geçirir; nihai candidate aynı kontrolü korur. Beş negatif senaryo (permission, tenant, company, actor ve atanmamış depo) için producer çağrı sayısının sıfır kalmasını denetleyen kontroller eklendi. Kabul ölçütü yetkisiz istekten producer okuması çıkmamasıdır. Yeni endpoint, migration, kayıt, audit olayı veya ekonomik sonuç yoktur; mevcut çağıran transaction/audit sorumluluğu korunur. Çalışma zamanı güvenlik kontrolleri kullanıcı kararıyla MP-04 toplu kapısına ertelenmiştir; derleme çalışma zamanı kanıtı sayılmaz.

- 5 Eylül 2026: Inventory-owned reservation state, versioned demand lineage, warehouse/base-UOM hedefi ve exact decimal consume/release/expiry geçişleri eklendi. Persistence ve available sonucu üretilmedi; bu nedenle eşzamanlılık veya over-reservation kanıtı henüz ileri sürülmüyor. Domain ve Unit Release derlemesi dar kapıdır; runtime MP-04 sonunda çalıştırılacaktır.
- 5 Eylül 2026: `INV-RES-002` authorization candidate eklendi. `inventory.reservation.create`, exact company scope ve transaction içinde authoritative olarak yüklenecek actor-bound warehouse evidence olmadan persistence adayı üretilemez. Candidate demand source'un gerçekten var olduğunu iddia etmez; bu doğrulama ileride Inventory orchestration'ın çağıracağı published module contract üzerinden yapılacaktır. Inventory Application ve Unit Release derlemeleri dar kapıdır; runtime MP-04 sonunda çalıştırılacaktır.
- 5 Eylül 2026: Candidate exact demand evidence ile sıkılaştırıldı. Evidence source type/id/line/version, item, base-UOM ve azami ayrılabilir miktarı taşır; stale source version, farklı ürün/birim veya talebi aşan reservation reddedilir. Evidence tipinin varlığı tek başına otorite değildir; persistence öncesi producer module published contract adaptöründen aynı transaction bağlamında yüklenmesi zorunlu kalır. Inventory Application ve Unit Release derlemeleri `0 warning/error`.
- 5 Eylül 2026: İlk producer contract olarak `Sales.Contracts` reservation-demand snapshot'ı ve Sales-owned PostgreSQL source eklendi. Yalnız confirmed exact order version ve `sales.order.confirm` permission/company scope altında satır yayımlar; Inventory Sales tablosu okumaz.
- 5 Eylül 2026: `INV-RES-003` Sales → Inventory demand adaptörü eklendi. Adaptör producer'ın döndürdüğü tenant/company/order/version bağlamını request ile birebir doğrulayıp her satırı Inventory quantity/UOM değer nesneleri üzerinden reservation evidence'a çevirir; null veya scope/version sapması fail-closed kalır. Exact order-line bulunmadan candidate kurulamaz; bulunan satırdan üretilen state ayrıca permission, company, actor-bound warehouse ve demand-capacity invariant'larından geçer. Inventory Infrastructure, Integration ve Architecture Release derlemeleri `0 warning/error`; runtime MP-04 toplu kapısına bırakıldı. Candidate builder persistence/available sonucu üretmez ve confirm orchestration hâlâ kapsam dışıdır.
