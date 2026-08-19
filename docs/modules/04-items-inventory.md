# INV — Stok, Malzeme ve Depo

## 1. Amaç

Ürün/hizmet kartı, birim/barkod, depo/bin, fiziksel ve rezerve miktar, giriş/çıkış/transfer/sayım, lot/seri izlenebilirliği ve stok değerini yönetir.

## 2. Ana varlıklar

- `Item`: stock/non-stock/service/expense tipi, base UOM, tax category, tracking policy.
- `ItemCompany`: Company bazlı aktiflik, muhasebe ve maliyet profili.
- `ItemUom`, `Barcode`, `ItemVariant` (faz kontrollü).
- `Warehouse`, `BinLocation` referansı ORG'den.
- `StockDocument`: Receipt, issue, transfer, adjustment, count adjustment.
- `StockMovement`: Append-only signed quantity/value.
- `Reservation`: Demand source'a ayrılan miktar.
- `Lot`, `SerialNumber`: İzlenebilirlik, expiry, source.
- `CostLayer` / `InventoryValuation`: Maliyet yöntemi.
- `CountSession`, `CountLine`: Sayım snapshot ve fark.

## 3. Miktar anlamları

```text
on_hand    = posted giriş - posted çıkış
reserved   = aktif rezervasyon toplamı
blocked    = kalite/hukuk/hasar nedeniyle kullanılamayan
available  = on_hand - reserved - blocked
expected   = açık PO/transfer kabul planı
```

Bu değerlerin her biri API/UI'da ayrı gösterilir. `stock` adlı belirsiz tek sayı kullanılmaz.

## 4. Hareket tipleri

- Satın alma kabulü.
- Satış sevki.
- Müşteri iadesi / tedarikçiye iade.
- Depolar arası transfer.
- Açılış/migration.
- Sayım farkı.
- Hasar/fire/consumption adjustment.
- Üretim Faz 3; ilk sürümde yok.

Her movement source belge/satır, warehouse/bin, quantity/UOM/base quantity, lot/serial, legal date, cost ve GL posting bağlantısı taşır.

## 5. Negatif stok

- Varsayılan hard block.
- Şirket politikası izin verirse yalnız belirli item/warehouse, permission, reason ve onay.
- Negatif hareket `cost_pending` olabilir; düzeltme worker'ı ayrı adjustment üretir, eski movement'i değiştirmez.
- Kapanış negatif stok varken bloklanabilir; rapor zorunlu.

## 6. Rezervasyon

Durum: `active → partially_consumed → consumed | released | expired`.

- Sales order/demand source ve line unique.
- Available kontrolü ve reservation create atomik.
- Kısmi sevk rezervasyonu azaltır.
- Sipariş iptal/reject/expiry release event'i üretir.
- Over-reservation policy varsayılan kapalı.
- Depo/lot seçimi sevk anında veya policy gereği rezervasyonda.

## 7. Transfer

1. Transfer request/approval.
2. Source warehouse issue posting.
3. Transit quantity (aynı anda teslim değilse).
4. Destination receipt ve fark/hasar.
5. Close.

Tek adımlı transferde source/destination movement aynı transaction. İki adımlı transferde transit sanal konum ve her aşama ayrı posted belge; toplam miktar zincirde açıklanabilir.

## 8. Lot ve seri

- Item tracking policy: none/lot/serial.
- Serial quantity her movementte tam adet ve unique lifecycle.
- Lot company + item içinde unique; supplier lot ayrıca.
- Expiry ve manufacture date validation; expired issue policy.
- Recall/trace raporu: tedarikçi kabulünden müşteri sevkine kadar.
- Tracking policy ilk movement sonrası geçmişe dönük değişmez.

## 9. Maliyet

MVP varsayılanı **hareketli ağırlıklı ortalama**; şirket/item cost profile bazlı. FIFO Faz 2 kararı olabilir.

- Receipt cost: base fiyat + dağıtılmış landed cost - iskonto + dahil edilebilir masraf.
- Issue, posting anındaki mevcut average cost snapshot'ı.
- Backdated movement cost yeniden hesaplama batch'i tetikleyebilir; kapanmış dönemi otomatik değiştirmez.
- Rounding/variance ayrı hesap ve movement.
- Stok miktarı ile değer ayrı invariants.
- Cost görünümü ayrı permission.

## 10. Sayım

`planned → frozen/snapshot → counting → review → posted → closed`.

- Blind count opsiyonu; kullanıcı beklenen miktarı görmez.
- Aynı item/bin/lot için ikinci sayım gerektiğinde policy.
- Snapshot sonrası hareketler ayrı tutulur ve as-of fark hesaplanır.
- Fark threshold'a göre depo + muhasebe onayı.
- Posted fark ayrı stock movement ve journal; count line update edilmez.

## 11. API

```text
GET/POST /api/v1/items
GET      /api/v1/inventory/availability
GET      /api/v1/items/{id}/movements
POST     /api/v1/stock-receipts
POST     /api/v1/stock-issues
POST     /api/v1/stock-transfers
POST     /api/v1/reservations
POST     /api/v1/count-sessions
POST     /api/v1/count-sessions/{id}/post
GET      /api/v1/lots/{id}/trace
```

## 12. UI

- Ürün listesi server filtre/sort; stok/maliyet kolonları role göre.
- Depo matrisi: on-hand/reserved/available/expected.
- Hareket timeline kaynak belge ve GL linki.
- Barkod girişi keyboard/scanner friendly; aynı barkod ambiguity bloklanır.
- Sayım ekranı offline mobil Faz 2; sync conflict açık.

## 13. Muhasebe

Posting örnekleri company mapping'den:

- Receipt: Inventory debit / GRNI veya supplier clearing credit.
- Shipment: COGS debit / Inventory credit.
- Count gain/loss: Inventory ↔ variance account.

Hesap kodu modül kodunda hard-code edilmez; posting rule ID snapshot edilir.

## 14. Raporlar

- Anlık ve as-of stok.
- Depo/bin/lot/seri hareketi.
- Rezerve/kullanılabilir/beklenen.
- Stok değer ve GL mutabakatı.
- Negatif, kritik, yavaş ve yaşlı stok.
- Sayım farkı ve onay.
- Lot recall/expiry.

## 15. Kabul testleri

- [ ] Transfer source -10, destination +10 ve toplam sıfır.
- [ ] 20 paralel reservation available miktarı aşmıyor.
- [ ] Kısmi sevk rezervasyonu ve sipariş kalanını doğru azaltıyor.
- [ ] Serial aynı anda iki depoda bulunamıyor.
- [ ] Count snapshot sonrası hareketle as-of fark doğru.
- [ ] Posted movement update/delete blok; reversal/adjustment izli.
- [ ] Stok valuation kontrol hesabı GL ile mutabık.
- [ ] Cost permission olmayan API/export kullanıcısına maliyet sızmıyor.

## 16. Değerleme politikası ve alt defter

Her Company + ItemCategory için perpetual veya periodic valuation policy sürümlü olarak seçilir. MVP’de perpetual desteklenecekse her fiziksel ekonomik olay StockLedgerEntry ve gerekiyorsa GL etkisi üretir; periodic destek yalnız açık kapsam/rapor/closing işlemleriyle eklenir. İki yaklaşım aynı şirkette sessizce karışamaz.

StockLedgerEntry en az source event/line, effective date, deterministic sequence, warehouse/location, owner, lot/serial, quantity, value, valuation rate, cost method ve projection generation taşır. QOH, reserved, available, expected, in-transit ve accounting quantity aynı alan değildir.

Geçmiş tarihli bir hareket FIFO/moving-average zincirini etkilerse:

1. etkilenen sonraki hareket/cost layer aralığı hesaplanır;
2. kapalı inventory/GL/tax dönemleri gösterilir;
3. dry-run eski/yeni değer ve GL farkı verir;
4. onaylı repost yeni generation üretir;
5. stok değeri ile GL inventory control account sıfır fark olur.

## 17. Sahiplik, kabul ve cut-off

Facility/location fiziksel custody; owner ekonomik sahipliktir. Konsinye veya yoldaki malda bu kimlikler farklı olabilir. Receipt, inspection acceptance ve ownership/risk transfer tarihleri ayrı tutulabilir. Goods in transit, received-not-invoiced ve invoiced-not-received raporları kapanışın zorunlu parçasıdır.

Landed cost navlun, sigorta ve benzeri adjustment’ı açık allocation basis ile receipt/cost layer’a dağıtır; kaynak tedarikçi faturası ve GL satırlarına drill-down verir.

## 18. Sayım protokolü

- CountPlan location/risk/frequency, assignee, cutoff watermark ve kör sayım seçeneği taşır.
- Beklenen miktar kör sayım tamamlanmadan gösterilmez.
- Sayım sırasında gerçekleşen hareketler watermark sonrası listelenir; sistem sessiz snapshot farkı yazmaz.
- Tolerans aşımı farklı kullanıcıdan recount ve gerekiyorsa onay ister.
- Count sonucu posted adjustment event üretir; on-hand kolonunu doğrudan overwrite etmez.
- Annual full count ile risk bazlı cycle count takvimi ayrı raporlanır.

Kabul testleri eşzamanlı transfer sırasında sayım, seri/lot cardinality, negatif stok policy, backdated valuation ve recount görevler ayrılığını içerir.
