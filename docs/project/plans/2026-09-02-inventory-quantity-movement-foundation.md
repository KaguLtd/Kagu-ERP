# MP-04 Inventory Quantity and Movement Foundation

- **Amaç:** Stok ve satış zincirinin politika seçmeyen ilk domain temelinde, veritabanı hassasiyetiyle uyumlu miktarı ve kaynak bağlı stok hareketini güvenli biçimde modellemek.
- **Master fazı ve kapısı:** MP-04 / giriş ve miktar korunumu çıkış kanıtı.
- **Risk sınıfı:** R4 — stok miktarı, tenant/company izolasyonu ve ilerideki GL değerleme zinciri.
- **Durum:** in-progress.
- **Sahip:** Ürün/muhasebe sahipleri `atanmadı`; teknik uygulama Codex.
- **Başlangıç:** 2 Eylül 2026.
- **İlgili requirement ID'leri:** `INV-INV-001`, `INV-MOV-001`, `INV-TRF-001`, `INV-BKD-001`, `INV-MST-001`, `DATA-002`.
- **Etkilenen modüller:** Inventory Domain ve domain kalite harness'i.
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
- Scope, kaynak, yön, hassasiyet ve transfer negatif testleri.

### Dahil değil

- Maliyet yöntemi, değerleme, GL posting, vergi veya hesap eşlemesi.
- Eksi stok/over-reservation izni.
- İki aşamalı transit, hasar/fark, lot/seri, sayım, backdate izni, değer farkı hesabı veya repost kararı.
- Persistence, API, web, Android ve production warehouse authoring.

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
| 4 | MP-04 persistence devam planı | `DEC-MP01-011` sınır değerlendirmesi | pending |
| 5 | Backdated impact-preview contract | Watermark, affected range ve tam lock-scope evidence | validating |
| 6 | Item master ve typed base UOM | Tenant/company scope, tracking ve quantity-scale negatifleri | validating |

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

## Tamamlanma kanıtı

- [ ] Domain kabul kriterleri ve dar testler.
- [x] Mimari sınır ve derleme.
- [x] Belgeler ve master etkisi.
- [x] Açık politika sınırlarının korunması.
- [x] Commit/push bu oturum sonunda kullanıcı talebiyle.
