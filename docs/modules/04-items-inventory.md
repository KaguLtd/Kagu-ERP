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

- `INV-RES-010`: `0048` consume/release olayları append-only ve ardışık sürümlüdür. Consumption
  kalanı aşamaz; release tüketileni korur ve yalnız aktif kalanı sıfırlar. Terminal olaydan sonra
  yeni geçiş reddedilir. Effective/occurred/recorded tarihleri ayrı tutulur; geriye giden geçişler
  kapalıdır. Otomatik expiry DEC-MP01-025 gereği açılmaz. Bakiye sorgusu seçili effective/recorded
  kesitte son olaydan active remaining ve demand için consumed+remaining hesaplar. Böylece release
  geçmiş tüketimi yeni rezervasyona açık miktara dönüştürmez. Runtime INSERT henüz kapalıdır.

- `INV-RES-009`: Internal reservation balance loader exact request/demand eşleşmesinden sonra
  source type/id/line için version ve depodan bağımsız demand lock, ardından stok position lock
  alır. Seçili effective date ve DB recorded cutoff ile on-hand; pozisyondaki ve talepteki brüt
  creation toplamlarını döndürür. Yetki bekleme sonrası yeniden yüklenir. Bu sonuç lifecycle
  düşümleri ve blocked miktar kaynağı bağlanana kadar available sayılmaz; dış API'ye yayımlanmaz.

- `INV-RES-008`: Reservation request gate exact tenant/company/request için transaction advisory
  lock alır; actor/depo/fingerprint lock anahtarını bölmez. ReadCommitted zorunludur. Kilit öncesi
  yetki ve depo kapsamı doğrulanır; bekleme sonrası yetki ve immutable replay sonucu yeniden okunur.
  Yeni istek sonucu null ise caller transaction açık tutularak demand ve position kilitlerine geçilir.
  Kilit sırası request → demand → topluca sıralı positions olmalıdır; bu gate available kanıtı üretmez.

- `INV-RES-007`: Request fingerprint formatı `inventory-reservation-request/v1`; tenant/company,
  request, actor, depo, exact source kimliği/sürümü, G29 invariant miktar, effective date ve policy
  sürümünü içerir. Retry loader güncel authoritative depo kapsamını yükler; eşleşen immutable
  sonucu sıfır miktar dahil döndürür, farklı içerikte generic conflict verir. Bulunmayan sonuç null'dır;
  bu tek başına yazma yetkisi değildir, writer request kilidi altında tekrar kontrol etmelidir.

- `INV-RES-006`: `0047` istek-sonuç kaydı sıfır sonucu da immutable saklar. Aynı company/request
  tek sonuç üretir; 64 haneli canonical SHA-256 fingerprint aynı anahtarın farklı içerikle
  kullanımını writer seviyesinde ayırt edecektir. Pozitif sonuç creation'a exact request, depo,
  miktar ve reservation kimliğiyle deferred FK taşır; creation da sonuç kaydını zorunlu tutar.
  Sıfır sonuç reservation kimliği taşımaz. Bu şema hâlâ SELECT-only'dir; replay writer henüz açık değildir.

- `INV-RES-005`: Oluşturma snapshot'ı immutable ve company-scoped kalır; request kimliği unique,
  reserved quantity pozitif ve requested quantity'yi aşmaz. Effective date, recorded timestamp,
  actor/correlation ve policy sürümü ayrı taşınır. `0046` şema adımı uygulamaya yalnız SELECT verir;
  runtime yazma capacity/idempotency orchestration tamamlanmadan açılmaz. Sıfır stok sonucu bu
  tabloda sıfır miktarlı rezervasyon olarak tutulmaz; retry sonucu için ayrı request kaydı gerekir.

DEC-MP01-025: Depo kullanıcı tarafından seçilir; mevcut miktar kadar kısmi rezervasyon yapılır,
kalanı açık sipariş olarak bekler. Otomatik expiry kullanılmaz; sevk tüketimi ve gerekçeli/yetkili
release ayrı geçişlerdir. Sipariş iptalinde kalan rezervasyon serbest bırakılmalıdır.

- `INV-RES-004`: Ayrılacak miktar `min(talep, max(0, on_hand - reserved - blocked))`;
  ayrılan + ayrılmayan = talep exact decimal korunur. Sıfır sonuç reservation kaydı oluşturmaz.
- `INV-LOCK-001`: Stok yazarları tenant/company/item/warehouse/base-UOM için ortak transaction
  advisory lock kullanır. Bütün hedefler tek çağrıda, sıralı kilit anahtarlarıyla alınır. Hash
  çakışması yalnız gereksiz sıralamaya neden olabilir. ReadCommitted zorunludur; miktar sorgusu
  kilit alındıktan sonra yapılmalıdır. Kilit yetki veya stok yeterlilik denetiminin yerine geçmez.
  Mevcut immediate-transfer writer protokole katılır; diğer writer'lar açılırken katılmalıdır.

Durum: `active → partially_consumed → consumed | released | expired`.

- `INV-RES-001`: Reservation exact tenant/company/item/warehouse/base-UOM ile versioned demand
  source type/id/line/version taşır. Consume yalnız aktif kalan miktar kadar ve exact decimal yapılır;
  release gerekçeli, expiry ise yalnız açık UTC expiry anı geldiğinde mümkündür. Her geçiş previous/new
  state+version, actor, correlation ve occurrence taşıyan append-only event üretir.
- `INV-RES-002`: Reservation create adayı `inventory.reservation.create`, exact company scope ve
  transaction içinde authoritative olarak yüklenmiş actor-bound warehouse evidence ister; yalnız UI
  görünürlüğü veya request içindeki ham warehouse listesi yetki kanıtı değildir. Candidate ayrıca
  üretici modülün published contract'ından yüklenen exact source version/item/base-UOM/azami miktar
  demand evidence'ıyla birebir eşleşir; caller beyanı talep kanıtı sayılamaz.
- `INV-RES-003`: Sales order demand, Inventory'nin Sales tablolarını doğrudan okumasıyla değil,
  `Sales.Contracts` altında yayımlanan immutable confirmed-order snapshot'ının Inventory-owned
  adaptöre çevrilmesiyle alınır. Adaptör dönen tenant/company/order/version bağlamını yeniden
  doğrular; eksik veya uyuşmayan producer sonucu fail-closed reddeder. Exact order line seçimi,
  reservation state kurulumu ve permission/company/warehouse evidence doğrulaması tek candidate
  builder sınırından geçer; bu builder persistence veya available sonucu üretmez. Builder permission,
  tenant/company ve actor-bound depo yetkisini producer çağrısından önce doğrular; yetkisiz istek
  sipariş talebini okumaz. Nihai candidate kurulurken aynı yetki kontrolü korunur.
- Sales order/demand source ve line unique.
- Available kontrolü ve reservation create atomik.
- Kısmi sevk rezervasyonu azaltır.
- Sipariş iptal/reject/expiry release event'i üretir.
- Over-reservation policy varsayılan kapalı.
- Depo/lot seçimi sevk anında veya policy gereği rezervasyonda.

Mevcut teknik dilim lifecycle, yetki ve producer-demand adaptör sözleşmesidir; persisted available
veya stok ayrıldığı iddiasında bulunmaz. Depo seçim zamanı ve position-lock politikası karara bağlanıp
Inventory-owned persistence tamamlanmadan Sales confirm rezervasyon oluşturamaz.

## 7. Transfer

1. Transfer request/approval.
2. Source warehouse issue posting.
3. Transit quantity (aynı anda teslim değilse).
4. Destination receipt ve fark/hasar.
5. Close.

Tek adımlı transferde source/destination movement aynı transaction. İki adımlı transferde transit sanal konum ve her aşama ayrı posted belge; toplam miktar zincirde açıklanabilir.

Kesinleşmiş tek adımlı transfer yerinde değiştirilmez. Düzeltme, ters yönde yeni bir transfer ve iki original movement'e birebir `reversal_of_movement_id` bağlantısı üretir; her original hareket yalnız bir doğrudan reversal alır ve karşı miktar aynı item/warehouse/base-UOM içinde exact olmalıdır.

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
