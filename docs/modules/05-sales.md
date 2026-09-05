# SALES — Tekliften Tahsilata Satış Süreci

## 1. Amaç

Teklif, satış siparişi, stok rezervasyonu, sevk/irsaliye, fatura, iade ve ilgili cari/stok/muhasebe etkilerini yönetir. Tahsilatın sahibi Treasury, açık kalemin sahibi PARTY'dir.

## 2. Varlıklar

- `SalesQuote` / line.
- `SalesOrder` / line / schedule.
- `PriceList`, `PriceRule`, `DiscountDecision`.
- `Dispatch` / line / package/carrier.
- `SalesInvoice` / line / payment schedule.
- `SalesReturnRequest`, `ReturnReceipt`, `CreditNote`.
- `SalesDocumentLink`: quote→order→dispatch→invoice→return zinciri.
- `FulfilmentAllocation`: Kaynak satır ile dönüşen satır miktarı.

## 3. Teklif

- Draft/review/approved/sent/accepted/rejected/expired.
- Fiyat ve vergi taslakları gösterilir; kabulde güncellik/validity yeniden doğrulanır.
- Revision numarası; gönderilen revision değişmez.
- Order dönüşümünde source revision saklanır.

## 4. Sipariş

- `SALES-ORD-001`: Sipariş lifecycle geçişi exact expected version, actor, UTC occurrence ve correlation olmadan uygulanamaz; her geçiş previous/new state ve version taşıyan append-only olay üretir.
- `SALES-ORD-002`: Sipariş commitment'tır; confirm tek başına stok hareketi, gelir, cari açık kalem veya GL kaydı üretmez.
- `SALES-ORD-002A`: İlk taslak 1–500 authoritative satırla atomik oluşturulur. Her satır immutable
  `order_line_id`, şirket için aktif item, canonical base UOM ve pozitif `numeric(20,6)` miktar taşır;
  API miktarı binary floating point'e çevirmeyen invariant decimal string olarak alır/verir. Aynı create
  idempotency kimliği farklı satırlarla tekrar kullanılamaz. Satır revizyonu ileride eski commitment'ı
  yerinde değiştirmeyen ayrı version/event sözleşmesiyle açılacaktır.
- `SALES-ORD-003`: Sipariş current projection'ı tenant/company forced RLS altında exact `+1`
  optimistic version ile güncellenir; her update aynı transaction'daki immutable transition
  event ile birebir doğrulanır. Correlation tekrarında immutable komut içeriği aynıysa ilk sonuç
  döner, farklıysa conflict oluşur.
- `SALES-ORD-004`: Sipariş detay okuması `sales.order.view` permission ve company scope ister;
  current state ile version sıralı transition timeline aynı caller transaction'ında okunur ve
  eksik, sıra dışı veya current state ile uyuşmayan geçmiş fail-closed reddedilir.
- `SALES-API-001`: Sipariş create ve transition HTTP komutları UUID `Idempotency-Key`, transition
  ayrıca quoted positive `If-Match` ister. Create anahtarı aggregate kimliği, transition anahtarı
  correlation kimliğidir; aynı immutable istek ilk sonucu döndürür. Başka scope kaynakları 404,
  stale version 412, iş/idempotency çatışması 409 ve domain validation 422 Problem Details üretir.
  Sözleşme build sırasında OpenAPI 3.1 olarak `docs/openapi/KaguERP.Api.json` dosyasına üretilir;
  operation kimlikleri, zorunlu header'lar ve cevap matrisi architecture drift kapısındadır.
  Aynı sözleşmeden sabitlenmiş OpenAPI Generator ile TypeScript ve Kotlin istemcileri üretilir;
  web same-origin BFF adaptörünü, Android ise explicit HTTPS base URL ve enjekte token provider'ını
  kullanır. Generated DTO'lar Sales domain veya UI modeli sayılmaz.
- `SALES-RES-001`: Inventory rezervasyon orchestration'ı Sales tablolarını doğrudan okuyamaz.
  Sales-owned published contract yalnız `sales.order.confirm` permission/company scope altında,
  `confirmed` durumdaki exact order version için order-line/item/base-UOM/decimal miktar snapshot'ı
  yayımlar; approved fakat unconfirmed veya stale version görünmez sonuç verir.

Durum:

`draft → submitted → approved → confirmed → partially_fulfilled → fulfilled → closed`.

İptal/reject ayrı.

- Cari, teslim/fatura adres snapshot; açık master değişimi eski siparişi değiştirmez.
- Currency, price list, payment term, tax determination context.
- Kredi risk ve iskonto policy submit/confirm anında.
- Confirm ile reservation policy çalışır.
- Mevcut teknik dilimde confirm yalnız authoritative satır varlığını doğrular; atomik reservation
  orchestration bir sonraki MP-04 dilimidir ve tamamlanana kadar stok ayırmaz.
- Backorder/kısmi sevk ve kapanan line reason.

## 5. Fiyat ve iskonto

Öncelik örneği sürümlü:

1. Sözleşme/müşteri-ürün fiyatı.
2. Müşteri grubu fiyat listesi.
3. Kampanya/tarih etkili kural.
4. Genel fiyat listesi.
5. Manuel fiyat — permission/onay.

- Sistem seçilen kural, base price, discount bileşenleri ve manual override'ı snapshot eder.
- Net fiyat server hesaplar.
- İskonto limiti kullanıcı/rol/category bazlı.
- Maliyet altı satış ayrı permission + onay; maliyet yetkisi olmayan kullanıcıya sayı sızdırmadan uyarı.

## 6. Sevk

- Yalnız confirmed ve kalan quantity.
- Warehouse scope, available/reservation, lot/serial/expiry validation.
- Pick/pack opsiyonel; MVP'de basit dispatch.
- `post` stok çıkışı üretir; dispatch posted değişmez.
- Kısmi sevk sipariş fulfilment allocation'ını günceller.
- Taşıyıcı ve teslim zamanı e-Fatura internet satış alanları için snapshot.

## 7. Fatura

Kaynak policy: order veya posted dispatch. Serbest fatura permission ile olabilir.

1. Kalan faturalanabilir miktarı atomik ayır.
2. Party/address/payment schedule/tax context doğrula.
3. Satır net, KDV ve toplamları server hesapla.
4. Document sequence ayır.
5. Post: sales invoice + PARTY open item + GL + EINV outbox hazırlığı.
6. e-Fatura sonucu ayrı lifecycle; ticari posting ile Daire kabul durumu karıştırılmaz.

Fatura `draft/approved/posted`; e-Fatura `not_required/pending/sent/accepted/rejected` ayrı state alanıdır.

## 8. Vergi

- TAX modülüne determination request: company, branch, party tax status, item tax category, legal date, place/supply context.
- Sales yalnız `TaxDecision` snapshot'ını kullanır.
- Posted invoice yeniden hesaplanmaz.
- İstisna/0 oran reason code ve legal source zorunlu.

## 9. İade ve düzeltme

- Orijinal invoice/dispatch line referansı.
- İade miktarı önceki net satışı aşamaz; yetkili exception ayrı.
- Fiziksel return receipt stok girişi; kalite/bloke konumu seçilebilir.
- CreditNote/borç-alacak fişi PARTY/GL/TAX karşı etkisi.
- Orijinal fatura değişmez.
- KKTC e-Fatura iptal süresi/alıcının onayı gerekiyorsa EINV akışı ayrıca.

## 10. Muhasebe örneği

Satış faturası:

- Debit: Accounts receivable.
- Credit: Sales revenue.
- Credit: Output KDV/vergi.

Sevk maliyeti:

- Debit: COGS.
- Credit: Inventory.

Hesaplar posting rule ile item/revenue/tax/party group ve boyutlara göre.

## 11. Yetkiler

- `sales-quote.create/send`.
- `sales.order.view/create/submit/approve/confirm/cancel/close`.
- `sales.fulfilment.record` yalnız persisted order-line→dispatch-line allocation evidence'ı
  hazır olduğunda açılır; şu anda Application sınırında fail-closed'dur.
- `sales.price.override`, `sales.discount.approve`, `sales.margin.view`.
- `dispatch.create/post` warehouse scope.
- `sales-invoice.create/post/reverse`.
- `sales-return.approve/post`.

Creator ≠ exceptional discount approver; dispatch ve invoice scope ayrı olabilir.

## 12. API

```text
POST /api/v1/sales-quotes
POST /api/v1/sales-quotes/{id}/send
POST /api/v1/sales-quotes/{id}/convert-to-order
POST /api/v1/sales-orders
GET  /api/v1/sales-orders/{id}?companyId={companyId}
POST /api/v1/sales-orders/{id}/submit
POST /api/v1/sales-orders/{id}/approve
POST /api/v1/sales-orders/{id}/reject
POST /api/v1/sales-orders/{id}/withdraw
POST /api/v1/sales-orders/{id}/revise
POST /api/v1/sales-orders/{id}/confirm
POST /api/v1/sales-orders/{id}/cancel
POST /api/v1/sales-orders/{id}/close
POST /api/v1/dispatches
POST /api/v1/dispatches/{id}/post
POST /api/v1/sales-invoices
POST /api/v1/sales-invoices/{id}/post
POST /api/v1/sales-returns
GET  /api/v1/sales-orders/{id}/document-chain
```

## 13. UI

- Satış workspace: line grid keyboard-first, cari risk sidebar, stok available, fiyat/iskonto açıklaması.
- Belge zinciri timeline ve kalan quantity/tutar.
- Posted belge read-only; “Düzelt” eylemi uygun reversal/return wizard açar.
- Server hesap sonucu ve client preview farkında server otorite, kullanıcıya fark açıklaması.
- Yazdırma resmi belge sayılmaz; belge durumu ve kopya etiketi görünür.

## 14. Raporlar

- Teklif→sipariş→sevk→fatura dönüşümü.
- Açık/backorder sipariş ve teslim gecikmesi.
- Satış/marj/müşteri/ürün/şube/currency.
- İskonto/override ve maliyet altı satış.
- İade nedenleri.
- Faturalanmamış sevk ve sevk edilmemiş sipariş.

## 15. Kabul senaryoları

- [ ] Sipariş 10, sevk 6, fatura 6; kalan 4 her zincirde doğru.
- [ ] Paralel iki sevk reservation/kalan 10'u aşmıyor.
- [ ] Kredi/iskonto limit aşımı doğru approver'a gidiyor.
- [ ] Posted fatura tek active PARTY open item ve dengeli GL üretiyor.
- [ ] Aynı post idempotency key çift fatura/numara üretmiyor.
- [ ] İade orijinali değiştirmeden stok/cari/vergi/GL karşı hareketi yaratıyor.
- [ ] EINV ret olsa da ticari posting state'i kanıtlı ve retry kuyruğu ayrı.
- [ ] Yetkisiz depo/maliyet/company verisi sızmıyor.

## 16. Taahhüt–gerçekleşme ve satır bağlantısı

- `SALES-FUL-001`: Partial/full fulfilment durumu yalnız aynı tenant/company/order kapsamındaki unique order-line→dispatch-line allocation miktarlarından türetilir; toplam base quantity sipariş satırını aşamaz.

SalesOrder bir commitment’tır; stok veya gelir olayını tek başına yaratmaz. Dispatch/Delivery stok ekonomik olayı, SalesInvoice alacak/vergi olayı, Payment nakit olayıdır. Bu olayların tarihleri ve durumları birleşmez.

OrderLine, DispatchLine, Receipt/acceptance kanıtı ve InvoiceLine arasında SourceLineLink kullanılır. UBL davranışıyla uyumlu olarak:

- bir sipariş satırı birden çok sevke bölünebilir;
- bir sevk satırı bir veya birden çok fatura satırına bağlanabilir;
- tek fatura satırı birden çok sevk satırını birleştirebilir;
- link miktar, UOM dönüşümü, net/tax/charge payı ve reversal bağlantısını taşır;
- ordered/fulfilled/invoiced/returned remaining değerleri link event’lerinden türetilir.

## 17. Fiyat, charge ve ödeme koşulları

İskonto, vergi, navlun, paketleme ve diğer charge/allowance; belge veya satır seviyesinde ayrı adjustment kaydıdır. Hesaplama sırası, base amount, yüzde/tutar, vergiye dahil olma ve posting account snapshot edilir. Net tutarı elle yazarak kaynağı kaybetmek yasaktır.

PaymentTerm faturada due schedule üretir; üç taksit üç açık kalemdir. Deposit/prepayment satış faturası veya teslim edilmiş gelir gibi gösterilmez; unapplied customer credit ve açık fulfillment bağlarıyla izlenir.

## 18. Cut-off ve istisnalar

- Sevk edilmiş fakat faturalanmamış, faturalanmış fakat sevk edilmemiş ve teslim teyidi eksik raporları kapanışta zorunludur.
- Revenue recognition kapsamı MVP’de yerel muhasebe politikasıyla “fatura/sevk/teslim” tetikleyicilerinden açıkça seçilir; kod varsayımı yapılmaz.
- Dispatch veya invoice üretim hatası ayrı exception state’tir; sipariş miktarı sessizce tamamlandı sayılmaz.
- İade; kabul/disposition sonucu restock, quarantine, scrap veya supplier-return seçer. Credit note, stock reversal ve tax correction bağları ayrı fakat atomik kuralla üretilir.

Golden senaryo; kısmi sevk, birleşik fatura, taksit, avans, iade, navlun/iskonto ve banka settlement’ına kadar tüm kaynak/alt defter/GL linklerini doğrular.
