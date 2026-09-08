# MP-04 Sevk hazırlama temeli

- Amaç: Kısmi sevk hazırlığında siparişin kalan miktarını aşan veya farklı siparişe ait satırları reddetmek.
- Faz/requirement/risk: MP-04, `SALES-DSP-001`, `SALES-DSP-002`, `SALES-DSP-003`, `SALES-FUL-001`, R4.
- Durum: validating (domain dilimi; persistence henüz kapsam dışı). Sahip: atanmadı.
- Okunan belgeler: MASTER_PLAN MP-04 ve kapı kuralları, AGENTS, docs/README, Sales ve Inventory modülleri, ortak iş akışları, PLANS.
- Ready: MP-03 teknik kapısı mevcut; miktar hazırlama sözleşmesi için koşullu hazır. Rezervasyon persistence ve değerleme kararları bu dilimin dışında.

## Kapsam ve tasarım

Immutable hazırlık; sipariş state/version, commitment ve fulfilment evidence ile oluşturulur. Ürün ve birim siparişten alınır. Pozitif decimal miktar satır kalanını aşamaz. Confirmed/partially-fulfilled durumları kabul edilir; evidence satırları commitment ile aynı olmalıdır. Bir hazırlıkta aynı sipariş satırı bir kez seçilir. Bu ilk sınır lot/depo bölünmesi seçmez.

Kalıcı sevk veya posted allocation üretmez. API, DB migration, audit olayı, maliyet, tarih/cut-off ve GL değişmez; bu katmanlar posting diliminde birlikte ele alınacaktır. Domain nesnesi permission veya authoritative DB evidence sayılmaz. İlerideki application katmanı scope/yetkiyi ve aynı transaction içinde güncel state/allocations'ı yeniden yüklemek zorundadır; iki paralel hazırlığın toplamını bu nesne garanti etmez.

## Kabul ve test

- Sipariş 10, önceki sevk 6: hazırlık 4 kabul; 4.000001 ret.
- Yanlış scope/version, farklı commitment/evidence, mükerrer veya bulunmayan satır, boş hazırlık ve uygun olmayan durum ret.
- Snapshot giriş koleksiyonu sonradan değişse bile değişmez.
- Dar derleme; runtime kontroller MP-04 toplu test kapısında. Faz tamamlandı sayılmaz.

## İlerleme

- 7 Eylül 2026 devam: `PostgresSalesDispatchPreparationLoader` ilk sevk için persisted sipariş state/line/timeline yükler. `dispatch.create` ve `sales.order.view` kontrolü sorgudan önce; scope/RLS ve header `FOR SHARE` mevcut Sales loader'ında uygulanır. Exact version, confirmed durum ve hiç fulfilment geçişi olmaması zorunludur. Allocation persistence yokken kısmi sipariş için boş evidence üretilmez. Başarı (10'dan 6 hazırlık, 4 kalan), stale version, yetki reddi ve başka-company not-found senaryoları entegrasyon harness'ine eklendi. Yeni migration/endpoint yok, query kayıt yazmaz; depo/available/GL ve posting yetkisi üretmez. Integration Release build 0 uyarı/0 hata; runtime ve RLS kanıtı MP-04 toplu kapısına ertelendi. Sonraki adım persisted fulfilment allocation ve atomik stok çıkışı bağımlılıklarıdır; bu loader posting açılmadan allocation kaynağına geçirilmelidir. Faz kapısı değişmedi, commit/push yapılmadı.

- 7 Eylül 2026 devam: Application `AuthorizedSalesDispatchPreparation` eklendi. `dispatch.create` ile exact tenant/company kontrolü domain değerlendirmesinden önce yapılır; actor scope'u sonuçta korunur. Yetkisiz rol, yanlış tenant/company ve başarılı kısmi hazırlık senaryoları eklendi. `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj -c Release --no-restore -v:q` 0 uyarı/0 hata; runtime güvenlik senaryoları MP-04 toplu kapısında çalıştırılacak. Depo, güncel DB evidence, transaction, audit ve concurrency doğrulaması henüz sağlanmadığından bu nesne kalıcı sevk izni değildir. Yeni endpoint/migration yok; master kapısı değişmedi. Sonraki adım authoritative sevk hazırlık sorgusu için persisted allocation bağımlılığını çözmektir. Commit/push yapılmadı.

- 7 Eylül 2026: Kapsam ve sınırlar kaydedildi. Sonraki adım domain sözleşmesi ve kabul senaryoları.
- 7 Eylül 2026: `SalesDispatchPreparation` eklendi. Exact version, scope, status/evidence uyumu, unique satır ve kalan miktar kontrolü ile siparişten ürün/birim kopyalanır. 10−6−4, miktar aşımı, yanlış sürüm/durum/şirket, olmayan/mükerrer/boş satır ve koleksiyon snapshot senaryoları Unit harness'e kaydedildi. `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj -c Release --no-restore -v:q`: 0 uyarı/0 hata. Runtime testleri kullanıcı talebiyle MP-04 sonuna ertelendi; concurrency/DB veya gerçek stok çıkışı kanıtlanmış değildir. Master faz kapısı değişmedi. Sonraki adım sevk hazırlama application permission/scope sınırıdır. Commit/push yapılmadı.
