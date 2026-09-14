# MP-04 doğrulanmış maliyet geçmişi yayını

- Amaç: Gerçek geçmiş yokluğu ile henüz yayımlanmamış/erişilemeyen maliyeti ayıran kalıcı okuma temeli.
- Faz MP-04 / INV-COST-002; R3. Sahip atanmadı. Durum validating (migration/runtime kabulü bekliyor).
- Ready: DEC-011 son maliyet ve geçmiş yoksa sıfır onaylı. INV-COST-001 evidence ve watermark var.
- Okunan kaynaklar: AGENTS, MASTER_PLAN MP-04, docs/README, veri mimarisi, Inventory maliyet,
  DEC-011, issue-cost-selection planı, mevcut 0051 RLS/immutable migration örneği.

## Kapsam / done when

0052 additive tablo immutable history publication saklar: scope/item/depo/UOM/currency,
cutoff/sequence/generation/checksum, Known snapshot veya explicit NoHistory. Numeric kolon
.NET decimal'in exact mantissa/scale sınırını korur; para yuvarlama kuralı seçmez.
Owner dışı yazım yok. Uygulama okuması ayrı inventory.cost.view ve actor warehouse scope ister.
Tam watermark eşleşmesi bulunmazsa unavailable; boş sorgu sonucu sıfır maliyet değildir.

Yeni fatura/valuation producer ve publication writer bu dilimde yok: sahte cost publication
üretilmez, eski alışın en son maliyet olduğu varsayılmaz. Runtime endpoint/DI, GL, maliyet
ortalaması, grant ataması ve geriye dönük düzeltme yok. Test fixture yayınları üretim kanıtı değildir.

## Güvenlik / operasyon / test

Forced RLS, immutable UPDATE/DELETE, master FK, explicit origin/cost/checksum kontrolleri.
Yeni tablo boşken eski uygulama etkilenmez. Rollback uygulama sürümünü geri almakla sınırlı;
yayımlanmış kayıt silinmez. Migration production startup'ta otomatik uygulanmaz.
Testler: exact known/zero, missing publication, scope/UOM/cutoff, immutability ve RLS.
Runtime MP sonunda; dar compile ayrı kanıt. Migration/restore ve gerçek DB kabulü açık kalır.

## Uygulananlar ve doğrulama — 12 Eylül

- 0052 kayıtlı ve Migrator embedded resource kapsamındadır; tablo, master FK, unique watermark,
  immutable trigger, decimal mantissa/scale CHECK, forced RLS ve runtime SELECT-only yazıldı.
  Migration uygulanmadı; uygulama/production rollerine fiilen grant verilmedi.
- Inventory-owned loader exact publication eşleşmesini mevcut WarehouseScopeLoader ile
  aktör/depo kapsamına bağlar. Eksik yayın, farklı currency/checksum/position sıfır dönmez.
  SQL/erişim hatası yakalanıp NoHistory'ye çevrilmez. Yeni permission yalnız iç contract kodudur;
  IAM kullanıcı ataması veya HTTP yolu eklenmedi. Çağıran servis audit sınırını tamamlamalıdır.
- PostgreSQL senaryoları: önce absent, sonra exact known decimal ve explicit zero; farklı
  cutoff pozisyonu/checksum/currency; cost permission yokluğu; şirket scope ve RLS negatifi;
  immutable UPDATE; negatif ve scale>28 reddi; owner/runtime privilege kataloğu.
  Test fixture'ı önceki MP-04 company/item/depo kaydını kullanır, production'da çalıştırılmadı.
- `dotnet build src/Erp.Migrator/KaguERP.Migrator.csproj -c Release --no-restore -v:q`:
  0 uyarı/hata, 1.64 sn.
- `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release
  --no-restore -v:q`: son derleme 0 uyarı/hata, 23.83 sn. `git diff --check` temiz.
- Runtime/DB/finansal ve yetki testleri MP-sonu kadansı nedeniyle çalıştırılmadı. SQL sözdizimi,
  decimal dönüşümü, RLS ve migration ileri uyumluluğu gerçek PostgreSQL kapısında doğrulanacak;
  derleme bunları kanıtlamaz. API/OpenAPI CodeIntegrity engeli tekrar denenmedi/bypass edilmedi.
- Satınalma faturası authoritative publisher, publication kaynak snapshot doğrulaması,
  yeni generation üretimi, maliyet hesaplama ve GL writer henüz yok. Tablo varlığı veya test
  kaydı bunların tamamlandığı anlamına gelmez. MP kapısı değişmedi; commit/push yok.
