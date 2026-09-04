# MP-04 Inventory Quantity and Movement Foundation

- **Amaç:** Stok ve satış zincirinin politika seçmeyen ilk domain temelinde, veritabanı hassasiyetiyle uyumlu miktarı ve kaynak bağlı stok hareketini güvenli biçimde modellemek.
- **Master fazı ve kapısı:** MP-04 / giriş ve miktar korunumu çıkış kanıtı.
- **Risk sınıfı:** R4 — stok miktarı, tenant/company izolasyonu ve ilerideki GL değerleme zinciri.
- **Durum:** in-progress.
- **Sahip:** Ürün/muhasebe sahipleri `atanmadı`; teknik uygulama Codex.
- **Başlangıç:** 2 Eylül 2026.
- **İlgili requirement ID'leri:** `INV-INV-001`, `INV-MOV-001`, `INV-TRF-001`, `INV-BKD-001`, `INV-MST-001`, `INV-AUTH-001`, `DATA-002`.
- **Etkilenen modüller:** Inventory Domain, Inventory Infrastructure, migrator ve kalite harness'leri.
- **Okunan zorunlu belgeler:** `MASTER_PLAN.md`, `docs/README.md`, teknik temel, repository yapısı, veri mimarisi, ortak iş akışları, organizasyon, stok ve satış modül sözleşmeleri.
- **Definition of Ready sonucu:** Koşullu hazır. `DEC-MP01-011` değerleme, eksi stok, backdate/repost ve sayım politikalarını açık bırakır; karar kaydı generic quantity invariant ve impact-preview contract çalışmalarına açıkça izin verir.

## Kapsam

### Dahil

- PostgreSQL `numeric(20,6)` ile kayıpsız eşleşen decimal miktar değer nesnesi.
- Tenant/company ve exact source event/line/version/purpose kimliği.
- Item, warehouse, UOM, effective date, UTC recorded timestamp ve deterministic sequence taşıyan stok hareketi taslağı.
- Giriş/çıkış işaret doğrulaması ve aynı anda teslim edilen depo transferinde kaynak çıkışı + hedef girişinin exact miktar korunumu.
- Geri tarihli hareket için valuation watermark, etkilenen pozisyon aralığı ve tüm dönem-kilit kapsamlarını gösteren immutable impact-preview contract.
- Tenant seviyesinde ürün tanımı, şirket bazlı aktivasyon ve optimistic concurrency version sözleşmesi.
- Base UOM, ürün tipi, lot/seri takip uygunluğu ve 0-6 basamak miktar ölçeği doğrulaması.
- Company-scoped depo, tenant ürün, şirket aktivasyonu ve append-only stok hareketi için PostgreSQL migration.
- RLS, master kimlik/kod değişmezliği, kaynak/pozisyon tekilliği ve deferred immediate-transfer korunumu.
- Execution scope zorunlu, transaction-bound ve canonical kaynak tekrarında immutable içerik karşılaştıran immediate-transfer writer.
- `inventory.transfer.post` permission ve hem kaynak hem hedef depo kapsamını zorunlu tutan application candidate.
- Tarih etkili IAM kullanıcı-depo ataması, aktör bazlı RLS ve transaction içinde authoritative scope evidence yükleyicisi.
- Effective-date ve UTC recorded-cutoff kesitlerinde append-only hareketlerden depo/ürün/base-UOM bazlı on-hand miktarı yeniden kuran yetkili sorgu.
- Kaynak event/line/version/purpose lineage'ını koruyan, bitemporal kesitli ve deterministik cursor sayfalı stok hareket zaman çizelgesi.
- Posted transferi yerinde değiştirmeden her iki orijinal harekete exact karşı hareket bağlayan append-only reversal zinciri.
- Scope, kaynak, yön, hassasiyet ve transfer negatif testleri.

### Dahil değil

- Maliyet yöntemi, değerleme, GL posting, vergi veya hesap eşlemesi.
- Eksi stok/over-reservation izni.
- İki aşamalı transit, hasar/fark, lot/seri, sayım, backdate izni, değer farkı hesabı veya repost kararı.
- API, web, Android ve production warehouse/scope authoring.

## Değişmezler ve güvenlik sınırları

- Miktar binary floating point kullanmaz; en çok altı ondalık basamaklı `decimal` kullanır.
- Hareket tenant/company/item/warehouse/source/UOM bağlamı olmadan kurulamaz.
- Tenant ürün tanımı farklı tenant şirket aktivasyonunda kullanılamaz; servis/masraf/non-stock ürün lot veya seri takibi alamaz.
- Seri takipli ürün tam adetlidir; kesirli miktar tanımlayamaz.
- Receipt pozitiftir, issue negatiftir; adjustment sıfır olamaz.
- Immediate transfer aynı tenant/company/item/UOM/source/effective date içinde farklı depolar arasında exact sıfır toplam verir.
- Bu dilim mevcut miktar veya maliyet bakiyesi üretmez ve negatif stok izni vermez.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Inventory Domain proje sınırı | Dar build + architecture discovery | completed |
| 2 | Quantity ve source identity | Hassasiyet/scope negatifleri | validating |
| 3 | Stock movement ve immediate transfer | İşaret ve exact transfer korunumu | validating |
| 4 | Generic quantity/movement persistence | Migration catalog, RLS, append-only ve deferred transfer testi | validating |
| 5 | Backdated impact-preview contract | Watermark, affected range ve tam lock-scope evidence | validating |
| 6 | Item master ve typed base UOM | Tenant/company scope, tracking ve quantity-scale negatifleri | validating |
| 7 | Transaction-bound immediate-transfer writer | Savepoint, idempotent replay, typed conflict ve scope | validating |
| 8 | Transfer permission ve warehouse scope | Permission/company/source+destination warehouse negatifleri | validating |
| 9 | Authoritative warehouse scope evidence | Tarih etkili IAM ataması, actor RLS, evidence eşleşmesi ve atamasız aktör negatifi | validating |
| 10 | Bitemporal on-hand quantity query | Permission, warehouse scope, effective as-of, recorded cutoff ve exact transfer toplamları | validating |
| 11 | Scoped movement timeline | Kaynak lineage, effective/recorded kesiti, depo scope'u ve kararlı cursor pagination | validating |
| 12 | Immediate transfer reversal | Çift reversal linki, tek ters kayıt, exact karşı miktar ve sıfıra dönen depo bakiyesi | validating |

## Test planı

- Unit/property: sınır miktarları, yedinci ondalık, overflow, işaret, scope ve transfer toplamı.
- Architecture: Inventory Domain'in altyapı veya başka modül bağımlılığı taşımaması.
- DB integration/migration/restore, concurrency, web ve Android: MP-04 validating paketinde.
- Golden: satış siparişi→rezervasyon→sevk→stok/GL zinciri sonraki dilimlerde.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-09-02 | `DEC-MP01-011` açık | Değer/maliyet/eksi stok uygulanamaz | Generic miktar ve impact-preview sınırıyla ilerle |
| 2026-09-02 | Transfer ilk dilimi immediate | Transit/hasar semantiği seçilmez | İki aşamalı transfer ayrı karar/dilim |
| 2026-09-02 | Backdated hareket değerlemeyi etkileyebilir | Sessiz post/repost finansal sonucu bozar | Preview yalnız etki kanıtı üretir; izin veya maliyet sonucu seçmez |

## İlerleme günlüğü

- 2 Eylül 2026: MP-03 birleşik otomasyon kapısı tamamlandı; master durum `validating`. MP-04 için karar gerektirmeyen ilk R4 domain dilimi seçildi.
- 2 Eylül 2026: Inventory Domain solution'a eklendi. Quantity, source identity, signed movement ve immediate transfer exact-zero modeli ile üç dar `INV-` kontrolü yazıldı. Inventory Domain ve Unit harness Release build'leri `0 warning/error`; architecture/API harness `23` source project için geçti. Yeni Unit DLL iki denemede Windows Application Control `0x800711C7` ile runtime öncesi engellendi; test zayıflatılmadı veya güvenlik politikası bypass edilmedi.
- 2 Eylül 2026: Geri tarihli hareket için effective-date + deterministic-sequence pozisyonu, valuation watermark generation/cutoff/source checksum kanıtı ve operational/inventory valuation/GL/tax/hard-legal kilit kapsamlarının tamamını zorunlu tutan immutable impact preview eklendi. Contract backdate'e izin vermez, maliyet sonucu veya repost üretmez. Inventory Domain ve Unit harness Release build'leri tekrar `0 warning/error` geçti; runtime kanıtı MP-04 toplu test kapısına bırakıldı.
- 2 Eylül 2026: UOM kodu typed/canonical değer nesnesine taşındı. Tenant ürün tanımı şirket aktivasyonundan ayrıldı; ürün tipi, base UOM, lot/seri uygunluğu, kesirli miktar ölçeği ve tenant/company/version sınırları `INV-MST-001` kontrolüne bağlandı. Maliyet profili ve persistence bu dilime alınmadı.
- 2 Eylül 2026: `0043_inventory_quantity_movement_foundation` migration'ı eklendi. Company-scoped warehouse, tenant item, item-company activation ve append-only signed stock movement tabloları; forced RLS, immutable code/scope + version guard'ları, exact base-UOM FK'si, source/position uniqueness ve commit sonunda iki hareket/sıfır toplam isteyen deferred immediate-transfer constraint'i içeriyor. Gerçek PostgreSQL için şirket sızıntısı, append-only privilege, başarılı transfer ve eksik transfer negatif senaryosu yazıldı. Migrator, Inventory Domain ve Integration harness Release build'leri `0 warning/error`; migration/runtime testleri kullanıcıyla kararlaştırılan MP-04 toplu kapısına ertelendi.
- 4 Eylül 2026: Inventory Infrastructure sınırı ve transaction-bound immediate-transfer writer eklendi. Writer execution scope'u fail-closed denetliyor; savepoint ile olası tek-taraflı insert'i geri alıyor, canonical source retry'sinde iki immutable movement'i karşılaştırıp ilk transferi döndürüyor ve farklı içerikte `INVENTORY_TRANSFER_PERSISTENCE_CONFLICT` üretiyor. Entegrasyon senaryosu create/replay/conflict akışına geçirildi. Inventory Infrastructure, Integration ve Architecture Release build'leri `0 warning/error`; ilk paralel build'in paylaşılan `obj` kilidi sıralı tekrar ile giderildi. Runtime MP-04 toplu kapısına ertelendi.
- 4 Eylül 2026: Inventory Application sınırı ve `AuthorizedImmediateStockTransferCandidate` eklendi. `inventory.transfer.post`, company scope ve hem kaynak hem hedef warehouse scope olmadan writer adayı üretilemiyor; writer artık yalnız bu authorization evidence'ını kabul ediyor. `INV-AUTH-001` permission/company/warehouse negatifleri yazıldı. Unit harness'teki linked Inventory source + referenced assembly çakışması tek normal project reference'a çevrildi. Unit, Integration ve Architecture Release build'leri `0 warning/error`; runtime MP-04 toplu kapısına ertelendi.
- 4 Eylül 2026: Ham warehouse ID listesinin güven sınırını aşması kapatıldı. `iam.user_warehouse_scope` tarih etkili ataması actor/company/tenant RLS ile eklendi; PostgreSQL loader transaction-local trusted context kurarak immutable evidence üretir. Candidate evidence'ın aktif actor/company/tenant ile birebir eşleşmesini ve iki deponun atanmasını ister. Atamasız actor RLS negatifi entegrasyon senaryosuna eklendi. Application, Infrastructure, Unit ve Integration Release build'leri `0 warning/error`; runtime ve migration kanıtı MP-04 toplu kapısına ertelendi.
- 4 Eylül 2026: `inventory.quantity.view` ile company ve authoritative warehouse kapsamını zorunlu tutan bitemporal on-hand sorgusu eklendi. Loader append-only hareketleri `effective_date <= as-of` ve `recorded_at <= UTC cutoff` kesitinde item/warehouse/base-UOM bazında exact toplar; sıfır bakiyeyi satır olarak üretmez. Yetki atamaları transfer write ve miktar read transaction'ında `FOR SHARE` ile yeniden yüklenip kilitlenir; stale/revoked evidence kullanılamaz. Cutoff öncesi görünmezlik ve transfer sonrası `-10/+10` yeniden üretimi gerçek PostgreSQL senaryosuna eklendi. Infrastructure, Unit ve Integration Release build'leri `0 warning/error`; runtime MP-04 toplu kapısına ertelendi.
- 4 Eylül 2026: `inventory.movement.view` permission'lı hareket zaman çizelgesi eklendi. Query exact item, effective-date aralığı, UTC recorded cutoff, 1–200 sayfa boyutu ve deterministik `(effective_date, recorded_at, warehouse, sequence, movement)` cursor'u taşır. Loader yalnız authoritative atanmış depoları okur, source event/line/version/purpose ile transfer karşı-depo bağını korur ve her sayfada yetkiyi yeniden kilitli kanıtlar. İki hareketli transferin iki tek-satırlık sayfada yinelenmeden, source lineage ve sıfır toplam korunarak okunması PostgreSQL senaryosuna eklendi. Infrastructure, Unit ve Integration Release build'leri `0 warning/error`; runtime MP-04 toplu kapısına ertelendi.
- 4 Eylül 2026: Immediate transfer düzeltmesi append-only reversal bağına taşındı. Her iki karşı hareket distinct `reversal_of_movement_id` taşımadan reversal çifti kurulamaz; DB aynı original movement'in ikinci kez terslenmesini unique index ile, item/warehouse/UOM/exact karşı miktar ve recorded-time sırasını deferred guard ile korur. Yanlış `9/-9` karşılık commit sonunda reddedilecek, doğru `10/-10` çiftinden sonra her iki depo on-hand bakiyesi sıfıra dönecek PostgreSQL senaryoları eklendi. Domain, Infrastructure, Unit ve Integration Release build'leri `0 warning/error`; runtime/migration kanıtı MP-04 toplu kapısına ertelendi.

## Tamamlanma kanıtı

- [ ] Domain kabul kriterleri ve dar testler.
- [x] Mimari sınır ve derleme.
- [x] Belgeler ve master etkisi.
- [x] Açık politika sınırlarının korunması.
- [x] Commit/push bu oturum sonunda kullanıcı talebiyle.
