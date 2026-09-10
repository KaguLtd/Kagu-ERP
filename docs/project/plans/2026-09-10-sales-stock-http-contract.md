# MP-04 stock-order HTTP bağlantısı — SALES-RES-006

- **Amaç:** Mevcut confirm/cancel HTTP eylemlerinin stok rezervasyonunu atlamadan aynı transaction sahibi servisten geçmesi.
- **Master fazı/kapı:** MP-04; ana yaşayan plan `2026-09-05-inventory-reservation-lifecycle-foundation.md`.
- **Risk:** R4; yetki, miktar, geriye uyumluluk ve idempotency.
- **Durum:** in-progress; OpenAPI/SDK üretimi Windows Application Control nedeniyle blocked.
- **Sahip:** Ürün/muhasebe atanmadı; teknik Codex. Başlangıç: 10 Eylül 2026.
- **Okunan sözleşmeler:** API-001–010 (`05-api-contracts.md`), SALES-RES-003–005, INV-RES-014, IAM permission/scope, ortak geçiş kuralları, güvenlik tehdit modeli.
- **DoR:** DEC-MP01-025 miktar kararları yeterli; yeni finansal politika seçilmez. Runtime DB/grant ve MP-04 test kanıtı hazır değildir. Bu dilim yazımı açmaz.

## Sözleşme ve uyumluluk

Rota `POST /api/v1/sales-orders/{orderId}/{action}` değişmez. Confirm/cancel artık lifecycle-only
gateway'e hiçbir durumda düşmez. Bootstrap bağlantı dizesi olsa da `UnavailableSalesStockOrderGateway`
kaydeder. Doğru yetki ve geçerli payload ile 503 `SALES_STOCK_ORDER_SERVICE_UNAVAILABLE` beklenir.
Bu bilinçli güvenlik kapısıdır, yeni özelliğin üretime açılması değildir.

Önceki yalnız company/reason içeren confirm/cancel istemcisi değişmeden çalışır denmez: yeni alanlar
gereklidir; geçersiz/eski payload 422, stok yetkisi yoksa 403 alır. Eski lifecycle-only başarı davranışı
korunmaz. Dış istemci release'i, sürüm/uyumluluk değerlendirmesi ve yeni SDK yayını yapılmadan bu
değişiklik deploy edilmemelidir. Diğer action'lar aynı serviste kalır; stock alanı gönderirlerse sessizce
yok sayılmaz, 422 döner. Mevcut web/Android uygulama kodunda generated klasörler dışında bu eylemlere
çağrı bulunmadı; bu kontrol dış istemci yokluğunun kanıtı değildir.

Başlıklar: tek canonical UUID `Idempotency-Key`; `If-Match: "3"` gibi quoted pozitif tam sayı.
Weak ETag, çoklu değer, artı işareti, boşluk ve baştaki sıfır reddedilir. Timestamp sunucudan
PostgreSQL-safe UTC alınır; effectiveDate istemcinin ayrı ISO belge/etkin tarihidir.

Confirm örnek gövdesi (kimlikler yalnız örnektir):

```json
{
  "companyId": "10000000-0000-0000-0000-000000000001",
  "effectiveDate": "2026-09-10",
  "reservationLines": [{
    "orderLineId": "20000000-0000-0000-0000-000000000001",
    "warehouseId": "30000000-0000-0000-0000-000000000001",
    "requestedBaseQuantity": "3.125"
  }]
}
```

1–500 unique satır; her satırda tek depo. Miktar string, en çok 14 tam/6 kesir basamağı, pozitif;
virgül, bilimsel gösterim, işaret ve fazla basamak kabul edilmez. Decimal parser'ın fazla basamağı
sessiz yuvarlamasına izin verilmez. Item/UOM, tenant/actor ve rezervasyon kimlikleri body'den alınmaz.
Cancel `companyId`, `effectiveDate`, 1–500 karakter `reason` taşır; `reservationLines` taşımaz.

Başarılı yanıt mevcut lifecycle alanlarını korur, confirm için `reservations`, cancel için `releases`
ekler. Bütün miktarlar string; reservation/event/request ID ve recordedAt korunur. ETag sonuçtaki
order version'dır. Generic lifecycle yanıtında iki yeni koleksiyon null olabilir.

## Yetki, audit ve hata

- Confirm: `sales.order.confirm` + `inventory.reservation.create`; cancel: `sales.order.cancel` + `inventory.reservation.release`.
- Company trusted scope; authoritative warehouse kontrolü gateway/persistence'ta korunur. API görünürlüğü yetki yerine geçmez.
- 400: hatalı temel header/action/company; 422: stock payload; 403: permission; 404: kapsam dışı/bulunamayan sipariş; 412: order/reservation version; 409: immutable request veya iş çatışması; 503: unavailable.
- Denied olaylar mevcut audit writer'dan geçer; hata logu yalnız teknik tip ve company ID taşır. SQL, reason veya payload loglanmaz.
- Request cancellation korunur. Commit bağlantı hatası kesin rollback demek değildir; aynı idempotency key ile tekrar gerekir.

## Yapılanlar ve test kapısı

HTTP routing, DTO/string decimal parser, safe error mapping, ETag sıkılaştırma ve kapalı DI kaydı eklendi.
`SalesStockOrderHttpContractCheck` fake gateway ile handler yönlendirme/negatif/serialization senaryolarını
hazırlar; gerçek DB kanıtının yerine geçmez. Eski gateway fallback, eksik inventory permission audit'i,
ordinary transition, malformed input ve 403/404/409/412/422/503 ayrımı denetlenir. Bootstrap'ın bağlantı
ayarından otomatik açılmaması için registration kontrolü yazıldı. OpenAPI testine yeni request/result
miktar alanları eklendi; mevcut eski generated artifact bu yeni kapıyı karşılamaz.

`dotnet build src/Erp.Api/KaguERP.Api.csproj -c Release --no-restore -v:q` C# aşamasından sonra OpenAPI
loader'da Sales.Application DLL için `0x800711C7` ile başarısız oldu (12 hata). Güvenlik politikası veya
OpenAPI hedefi kapatılmadı; başka loader üzerinden engel aşılmadı. Sonraki ek API/test değişikliklerinin
tam derlemesi de henüz kanıtlanmadı. Bootstrap Release dar build 0 uyarı/0 hata (4,55 sn).
Runtime testleri kullanıcı MP-sonu kadansı gereği çalıştırılmadı.

## Sıradaki kesin adımlar / done when

10 Eylül devam incelemesi: Windows CodeIntegrity/Operational olay 3077 (09:55:59 ve 09:56:00),
Sales.Application DLL yüklemesinin imza/kod bütünlüğü politikası tarafından engellendiğini doğruladı.
Salt okunur olay kaydı incelemesi yapıldı; politika değiştirilmedi ve aynı engelli üretim tekrar denenmedi.
API yazımı hâlâ kapalıdır. Bundan bağımsız `SALES-RES-007` ile boş rezervasyon iptalinin etkin tarih
idempotency boşluğu düzeltildi; ana görev planında 0050 migration/test borcu kayıtlıdır.

1. Uygulama Denetimi engeli güvenlik politikasına uygun biçimde çözüldükten sonra normal API/Architecture build.
2. OpenAPI'yi normal üreticiden yenile, diff'i incele; `scripts/generate-api-clients.mjs` ile TS/Kotlin SDK'ları üret ve değişikliklerini incele. Eski şemadan SDK üretme; generated dosyaları elle uydurma.
3. MP-04 toplu kapıda handler, gerçek HTTP auth/CSRF, owner ve app RLS/grant, transaction/concurrency/idempotency testlerini çalıştır; sonuçlara göre düzelt.
4. Geriye uyumluluk/release kararı ve tüm kapılar tamamlanınca sınırlı SQL grant ve gerçek gateway registration. Production/deploy bu oturum kapsamında değildir.

Migration/yeni bağımlılık/secret yoktur. Rollback eski binary ile veri korunarak yapılabilir, fakat
legacy confirm/cancel fallback'inin geri açılması stok güvenliği açısından ayrıca değerlendirilmelidir.
Ana master fazı ilerletilmedi; commit/push yapılmadı.
