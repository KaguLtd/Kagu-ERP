# MP-04 satınalma faturası maliyet girdisi köprüsü

- Amaç: Invoice/receipt allocation kaynaklarını koruyan maliyet girdisini Inventory'ye taşımak;
  doğrulanmış tutar/miktardan açık sürümlü yuvarlama ile birim maliyet hesaplamak.
- Faz MP-04 / INV-COST-005, PUR-COST-001; risk R3; durum validating (contract/hesap dilimi); sahip atanmadı.
- MP-05 satınalma workflow'u açılmaz; bu ince Contracts bağımlılığı MP-04 maliyet kaynağı içindir.
- Ready: DEC-011 invoice cost source + zero/last-known kararları onaylı; miktar/para exact
  precision ve modüller arası Contracts sınırı mevcut. SQL producer henüz yok.
- Okunanlar: AGENTS, MASTER_PLAN MP-04, docs/README, repository/veri mimarisi,
  Purchasing modülü, Inventory maliyet, DEC-011 ve cost publication planları.

## Kapsam / done when

1. Purchasing.Contracts dependency-free source query/snapshot: exact invoice version,
   recorded cutoff, finalized source ve allocation→invoice-line→receipt-line lineage.
   Item/depo/UOM, numeric(20,6) miktar, numeric(20,4) eligible functional cost, FX ve cost-rule
   snapshot kimlikleri korunur. Bilinmeyen/henüz doğrulanmamış kaynak null/unavailable'dır.
2. Inventory adapter result scope/version/date ve tüm girdileri doğrular; Sales/Purchasing
   tablosu okumaz. Invoice unavailable hiçbir zaman NoHistory kanıtı değildir.
3. Birim maliyet explicit rounding policy ID + scale ile exact rational hesaplanır;
   AwayFromZero DEC-MP01-006 doğrultusunda midpoint uygulanır, ara decimal bölme yuvarlaması yok.
   Kaynak numerator/denominator saklı kalır. Bu tutar GL satırı veya hareketli ortalama değildir.
4. Sözleşme/adapter ve hesap unit senaryoları, dar derleme ve belge.
5. Bootstrap audited preview: source factory aynı caller connection/transaction ile kurulmalı;
   güncel depo scope tekrar doğrulanmalı, hesap/audit hatası bütün hazırlığı geri almalı.

## Sınırlar

Fatura/FX/KDV/landed-cost doğrulaması ve eligible cost dağıtımı Purchasing producer'ının
sorumluluğudur; bu proje onu tamamlamaz. Client tarafından gelen snapshot güvenilir sayılmaz.
Source implementation trusted scope ve caller transaction'a bağlanmadan DI açılmaz.
Şirket cost-scale politikası otomatik seçilmez, mevcut yuvarlama policy provider'ından gelmelidir.
Yeni nuget yok; ek proje Contracts ve mevcut BigInteger kullanır. DB, migration, HTTP/SDK,
GL ve kullanıcı permission değişikliği yok; maliyet farkı hesabı varsayılmaz.

## Test / risk

Scope/version/cutoff mismatch; empty/duplicate/bounded allocation; negative/overscale input;
receipt lineage; missing source != zero; exact midpoint/overflow/zero; snapshot immutability.
Runtime kullanıcı MP-sonu kadansında. Supplier invoice persistence olmadığı için gerçek
fatura SQL, authorization ve GL golden testleri ayrıca açık kalır.

## Sonuç ve kanıt

- Purchasing.Contracts projesi solution'a, Inventory Infrastructure project reference'ına ve
  restore lock graph'ına eklendi; ek NuGet bağımlılığı yok. Sözleşme ve immutable adapter
  scope/version/cutoff/depo kontrolü, tutarlı source-line kimlikleri ve bounded allocations içerir.
- Exact invoice cost basis, rational unit-cost hesaplayıcı ve source→basis→calculate→audit
  Bootstrap preview eklendi. Kaynak invoice producer ve şirket policy authority hâlâ yok;
  hesap sonucu 0052'ye otomatik yayımlanmıyor, hareketli ortalama yerine geçirilmiyor.
- Unit senaryoları 1/3, midpoint 1/8, fractional quantity, legitimate zero, 28 scale,
  büyük exact değer, overflow/invalid policy ve 2.525 bounded miktar/tutar örneğini içerir.
- Contract fixture'ları scope/version/cutoff sapması, null source, missing permission/depo,
  başka aktör evidence'ı, missing receipt, duplicate/inconsistent/500–501 allocation ve
  cancellation senaryolarını kapsar. PostgreSQL harness'ine bağlı preview fixture'ında gerçek
  IAM/audit transaction ile factory bağlantısı ve audit SQL failure rollback hazırlanmıştır;
  invoice source sentetiktir, gerçek fatura SQL testi değildir. Runtime çalıştırılmadı.
- İlk restore, sandbox varsayılan cache'inde Npgsql yokluğu ve nuget.org erişimsizliği nedeniyle
  NU1801/NU1101 ile başarısız oldu. Mevcut `C:\Users\ahmet\.nuget\packages` keşfedildi;
  integration, Bootstrap ve solution restore'u bu mevcut cache `--packages`/`--source` ile
  başarılı oldu. Yeni paket indirilmedi, global NuGet ayarı veya güvenlik kapısı değiştirilmedi.
  Çevrimdışı restore yeni çevrimiçi güvenlik taraması kanıtı değildir; CI kapısı korunur.
- `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj -c Release --no-restore -v:q`:
  0 uyarı/hata, 3.62 sn.
- `dotnet build src/Erp.Bootstrap/KaguERP.Bootstrap.csproj -c Release --no-restore -v:q`:
  0 uyarı/hata, 4.15 sn.
- Son `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release
  --no-restore -v:q`: 0 uyarı/hata, 5.99 sn. Test çalıştırması değil derleme kanıtıdır.
- Runtime/finansal/güvenlik testleri kullanıcı MP-sonu kadansı nedeniyle ertelendi;
  API/OpenAPI CodeIntegrity engeli tekrar denenmedi/bypass edilmedi. Yeni migration, DB yazımı,
  kullanıcı yetkisi, HTTP/DI aktivasyonu veya commit/push yok. Master kapısı değişmedi.
