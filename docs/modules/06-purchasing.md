# Satın Alma Modülü

## 1. Amaç ve kapsam

Satın alma modülü; talebin doğmasından mal veya hizmetin teslim alınmasına, tedarikçi faturasının doğrulanmasına ve ödeme önerisinin oluşmasına kadar olan süreci yönetir. Muhasebe kaydı, ödeme ve stok hareketi bu modül tarafından doğrudan yazılmaz; ilgili modüllere onaylı ve izlenebilir komutlar gönderilir.

Kapsam:

- satın alma talebi ve bütçe/limit kontrolü,
- teklif isteme ve teklif karşılaştırma,
- satın alma siparişi,
- mal kabulü veya hizmet kabulü,
- tedarikçi faturası kaydı,
- iki/üç yönlü eşleştirme,
- iade, fiyat farkı ve masraf dağıtımı,
- ödeme önerisi,
- tedarikçi performans raporları.

## 2. Temel varlıklar

`PUR-DRAFT-001` / MP-04 maliyet bağımlılığı: `purchasing.invoice_capture` ve normalleştirilmiş
satırları ilk fatura yakalamasını immutable sürüm 1 olarak saklar. Dış belge numarası/tarihi,
cari referansı, işlem dövizi, item/UOM, exact miktar ve net tutar taşır. Bu bir taslaktır;
tedarikçi rolü/cari döviz uygunluğu/aktif master, KDV/kur/matching, dönem veya final fatura
numarası tekilliği doğrulanmış sayılmaz. Bunlar tamamlanmadan işlem veya maliyet kaynağına
dönüşmez; `ISupplierInvoiceCostSource` bu capture tablosuna bağlanmamıştır.

İç writer `purchasing.invoice.create` ve exact actor/company kapsamı ister. Aynı capture ID
advisory lock altında canonical fingerprint ile replay edilir; orijinal kayıt zamanı korunur.
Header/satırlar ve audit outer transaction'da atomiktir. 0053 FK/deferred toplam-satır sayısı,
immutable trigger ve forced company RLS içerir; runtime rol SELECT-only kalır. Yeni permission
atanmaz, HTTP/DI açılmaz. Taslak revizyon/iptal ve finalized invoice source ayrı sonraki işlerdir.

`PUR-DRAFT-002`: İç okuma katmanı `purchasing.invoice.view` ile tek statement snapshot'ından
başlık/satırları yükler; orijinal yazar kimliğini koruyarak sürüm, toplam, satır sayısı ve
fingerprint'i doğrular. Başka tenant/company veya olmayan kimlik aynı null sonucu verir;
eksik/bozuk içerik genel integrity hatasıyla reddedilir. Okuma ve not-found audit'i caller
transaction içindedir; audit yazılamazsa sonuç dönülmez. Bu doğrulama ticari/mali onay değildir.

`PUR-DRAFT-003`: İç master kontrolü, kayıtlı capture'ı okuyup Parties'in yayımlanmış
participant'ıyla aynı şirkette payable hesap ve birebir döviz uyumunu; Inventory participant'ıyla
aktif ürün/şirket ataması, base UOM ve miktar ölçeğini kontrol eder. `purchasing.invoice.view`
zorunludur; master satırları caller RC transaction süresince FOR SHARE tutulur. Hata, bu
çağrının okuma audit'ini de savepoint'e geri alır. Geçmiş capture okuması bu güncel kontrollerden
bağımsız kalır. Cari aktifliği/tarih etkili supplier role mevcut şemada henüz modellenmediği
için doğrulanmaz. Stock/service/expense ayrımı ve lot/seri receipt uygunluğu bu sınırlı
kontrolün sonucu değildir; finalize, matching, vergi/kur/dönem ve maliyet kapıları açık kalır.

| Varlık | Önemli alanlar |
|---|---|
| `purchase_request` | şirket, şube, talep eden, ihtiyaç tarihi, para birimi, durum |
| `purchase_request_line` | stok/hizmet, miktar, birim, tahmini fiyat, masraf merkezi |
| `request_for_quote` | tedarikçiler, son tarih, şartlar |
| `supplier_quote` | tedarikçi, fiyat, teslim tarihi, geçerlilik, ek dosya |
| `purchase_order` | tedarikçi, teslim adresi, ödeme şekli, vergi/fiyat özeti |
| `goods_receipt` | depo, sipariş, teslim eden, lot/seri, kabul/red miktarı |
| `service_acceptance` | hizmet dönemi, kabul eden, ilerleme yüzdesi |
| `supplier_invoice` | belge no/tarih, tedarikçi, KDV özeti, eşleştirme durumu |
| `purchase_match` | sipariş–kabul–fatura farkları ve çözüm kararı |
| `purchase_return` | kaynak kabul, iade nedeni ve sevk bilgisi |

Her belge `company_id`, gerekliyse `branch_id`, `version`, `created_at`, `created_by` ve benzersiz iş numarası taşır.

## 3. İş akışları ve durumlar

### 3.1 Talep ve sipariş

`draft → submitted → in_approval → approved → sourced → ordered → closed`

`rejected` ve `cancelled` son durumları gerekçe ister. Onaydan sonra ticari alanlar yerinde değiştirilmez; revizyon yeni sürüm veya değişiklik emri olarak tutulur.

### 3.2 Mal kabul

- Siparişsiz kabul, yalnızca açıkça tanımlanmış istisna rolü ve zorunlu gerekçeyle yapılabilir.
- Kısmi teslimat desteklenir; kalan miktar sipariş üzerinde görünür.
- Fazla teslimat, yapılandırılabilir toleransı aşarsa onay ister.
- Lot/seri izlenen mal kabulünde kimlikler tamamlanmadan belge sonuçlandırılamaz.
- Kabul kesinleştiğinde stok modülüne idempotent hareket komutu gönderilir.

### 3.3 Üç yönlü eşleştirme

Sipariş miktar/fiyatı, kabul miktarı ve fatura miktar/fiyatı karşılaştırılır. Toleranslar şirket, tedarikçi, mal grubu ve para birimi bazında, yürürlük tarihli politika olarak saklanır.

Sonuçlar: `matched`, `within_tolerance`, `exception`, `blocked`, `resolved`.

`exception` durumundaki fatura ödeme önerisine giremez. Çözüm; ek kabul, iade, tedarikçi alacak dekontu, fiyat farkı onayı veya faturanın reddi olabilir.

## 4. Değişmez kurallar

- `PUR-INV-001`: Aynı tedarikçi + belge türü + belge numarası + mali yıl birleşimi yinelenemez.
- `PUR-INV-002`: Sipariş toplamı, yetki limitini aşan kullanıcı tarafından tek başına onaylanamaz.
- `PUR-INV-003`: Talep eden kişi, tanımlanan tutarın üzerindeki kendi talebinin nihai onaycısı olamaz.
- `PUR-INV-004`: Kesinleşmiş kabulte miktar doğrudan değiştirilemez; ters kabul/iade gerekir.
- `PUR-INV-005`: Ödeme banka hesabı değişikliği ayrı doğrulama ve çift kontrol ister.
- `PUR-INV-006`: Muhasebeleştirilmiş fatura silinemez; ters belge/iadeyle düzeltilir.
- `PUR-INV-007`: Her kaynak belge ile oluşan stok ve muhasebe kayıtları arasında iz sürülebilir bağlantı bulunur.

## 5. Roller ve yetkiler

- Talep sahibi: taslak/talep gönderme ve kendi taleplerini izleme.
- Satın alma uzmanı: teklif, karşılaştırma ve sipariş hazırlama.
- Satın alma yöneticisi: limit dahilinde sipariş onayı ve istisna çözümü.
- Depo görevlisi: fiziksel kabul/iade; fiyat ve banka bilgisine erişmez.
- Finans uzmanı: fatura kontrolü ve ödeme önerisi.
- Muhasebe: hesap/vergisel kontrol ve kayıt.
- Denetçi: salt okunur belge, akış ve değişiklik geçmişi.

Yetkiler şirket/şube/depo kapsamıyla ve işlem tutarıyla birlikte değerlendirilir.

## 6. Muhasebe ve diğer modüllerle bağlantı

### Maliyet girdisi sözleşmesi — `PUR-COST-001`

Inventory tarafındaki INV-COST-008 strict receipt doğrulaması için gelecekteki physical kabul
writer'ı `purchasing.goods-receipt` source type ve `receipt` posting purpose, exact kabul/line/version
kimliğini kullanır. Fatura allocation→movement ID/version bağı trusted source tarafından sağlanır.
Bu isimlendirme yeni ticari/muhasebe politikası veya uygulanmış mal kabulü writer'ı değildir.

`PUR-COST-002`: Source interface artık yalnız `ReconciledSupplierInvoiceCost` döndürür.
Yayımlanan allocation toplamları aynı query scope/version/cutoff bağlamındaki bağımsız fatura
satırı bütçelerine miktar ve eligible cost olarak exact eşit olmalıdır. Kabul satırı bazında
bütün invoice-line kullanımları toplanır ve available base quantity aşılmaz. Kapasite bu faturanın
kendi allocation'ı hariç önceki kesinleşmiş kullanımlardan sonra kalan miktardır; retry kendi
kullanımını iki kez düşmez. Eksik/fazla/duplicate kaynak satırı, yanlış item/UOM/depo veya
uyuşmayan source identity reddedilir. Listeler kopyalanarak salt okunur saklanır.

Bu matematiksel kanıt DB provenance kanıtı değildir: gerçek producer fatura/kabul bakiyelerini
aynı transaction'da bağımsız authoritative kayıtlardan kilitleyerek okumalıdır. Allocation'lardan
aynı bütçeleri yeniden türetmek yalnız sentetik fixture'da kullanılır, production'da geçerli
doğrulama değildir. Receipt capacity tüketimi/idempotency persistence, matching lifecycle ve
invoice SQL producer henüz yoktur; bu sözleşme bunları tamamlanmış saymaz.

MP-04 maliyet bağımlılığı için dependency-free Purchasing.Contracts açılmıştır; bu MP-05
satınalma workflow/persistence kabulü değildir. Transaction-bound producer exact tenant/company,
fatura kimliği/sürümü ve recorded cutoff için kesinleşmiş fatura maliyet snapshot'ı yayımlar.
Her allocation; fatura satırı, mal kabulü/satırı, item/depo/UOM, base quantity ve doğrulanmış
eligible functional cost taşır. FX ve cost-rule snapshot kimlikleri zorunludur. Producer,
allocation toplamlarının gerçek fatura/kabul miktar ve maliyet sınırlarına uyduğunu doğrulamalıdır.

Sözleşme 1–500 immutable allocation, unique kimlik ve invoice-line/receipt-line bağı,
aynı kaynak satırında tutarlı ürün/UOM/depo, numeric(20,6) miktar ve numeric(20,4) maliyet ister.
Fatura satırı birden çok kabule bölünebilir; aynı bağlantı yinelenmez. İndirilebilir KDV,
kur veya masraf uygunluğu Inventory'de tekrar hesaplanmaz. Fatura önce gelmiş fakat ilgili
mal kabul bağı olmayan veri bu sözleşmeyle stok maliyetine çevrilmez.

Source null/unavailable sonucu "maliyet geçmişi yok" değildir. Purchasing-owned SQL producer,
fatura lifecycle/matching ve gerçek source authorization henüz uygulanmamıştır; public DI
veya endpoint açılmaz. Fixture source gerçek fatura doğrulamasının kanıtı değildir.

- Mal kabulü: stok hareketi; şirket politikasına göre geçici kabul hesabı.
- Tedarikçi faturası: cari borç, indirilecek KDV, stok/gider/sabit kıymet ve kur farkı.
- İade: kaynak kaydı tersleyen bağlantılı stok ve muhasebe hareketleri.
- Ödeme önerisi: bankacılık modülünde onaylı ödeme emrine dönüşür.
- Masraf dağıtımı: navlun/sigorta/gümrük gibi giderleri stok maliyetine veya gider merkezine dağıtır; yöntem ve yuvarlama saklanır.

Kayıt şablonları muhasebe modülünde sürümlenir; satın alma koduna hesap numarası gömülmez.

## 7. API ve ekranlar

Örnek uçlar:

- `POST /api/v1/purchase-requests`
- `POST /api/v1/purchase-orders/{id}/submit`
- `POST /api/v1/goods-receipts/{id}/complete`
- `POST /api/v1/supplier-invoices/{id}/match`
- `POST /api/v1/purchase-exceptions/{id}/resolve`
- `GET /api/v1/purchasing/open-commitments`

Komut uçlarında `Idempotency-Key` ve sürüm tabanlı iyimser kilit kullanılır. Ekranlar: talep çalışma alanı, teklif karşılaştırma matrisi, sipariş sayfası, hızlı kabul, fatura/eşleştirme masası, istisna kuyruğu ve tedarikçi performansı.

## 8. Raporlar

- açık talep/sipariş ve gecikmeler,
- satın alma fiyat değişimi,
- taahhüt ve bütçe tüketimi,
- eşleştirme istisnaları,
- tedarikçi teslimat/kalite performansı,
- siparişsiz satın alma oranı,
- bekleyen iade ve alacak dekontları.

## 9. Kabul testleri

- Kısmi teslimatların kalan sipariş miktarını doğru hesaplaması.
- Yinelenen tedarikçi faturasının eşzamanlı iki istekte de engellenmesi.
- Tolerans altı/üstü fiyat ve miktar farklarının doğru sınıflanması.
- Yetki limiti ve görevler ayrılığı ihlallerinin reddedilmesi.
- Kabul terslemesinin stok ve muhasebe izini eksiksiz üretmesi.
- Farklı şirket/depo kullanıcılarının birbirinin verisini görememesi.
- Kur, KDV ve yuvarlama örneklerinin muhasebe toplamıyla mutabık olması.

## 10. Kapsam dışı / sonraki faz

Tedarikçi portalı, gelişmiş ihale, e-satın alma ağı, otomatik talep tahmini ve sözleşme yaşam döngüsü ilk sürümde kapsam dışıdır; entegrasyon noktaları korunur.

## 11. Eşleştirme politikası ve istisna yaşam döngüsü

MatchingPolicy ürün/hizmet ve tedarikçi riskine göre sürümlenir:

| Politika | Karşılaştırma | Uygun kullanım |
|---|---|---|
| 2-way | purchase order ↔ supplier invoice | kabul belgesi olmayan kontrollü hizmet/masraf |
| 3-way | order ↔ receipt ↔ invoice | fiziksel mal ve miktar kontrolü |
| 4-way | order ↔ receipt ↔ inspection acceptance ↔ invoice | kalite/uygunluk kapılı mal |

Ordered quantity ile received quantity üzerinden faturalama seçimi açık policy’dir. Tolerans; miktar, birim fiyat, toplam, vergi, kur ve tarih için ayrı olabilir. Tolerans dışı satır PaymentHold ve MatchException üretir; override permission, gerekçe, farklı onaylayan ve kanıt ister. İstisna çözülmeden otomatik ödeme önerisine girmez.

## 12. Teslim–fatura zaman farkı ve dönem sonu

- Goods receipt ekonomik stok/masraf olayını ve policy’ye göre GRNI/accrual etkisini üretir.
- Supplier invoice, receipt linkleri üzerinden GRNI’yi kapatır; bağımsız AP open item/vade kalemleri oluşturur.
- Fatura önce gelirse invoiced-not-received/ön ödeme veya açık exception policy’si uygulanır; stok varmış gibi yazılmaz.
- Hizmet kabulü miktar yerine milestone/service-entry kanıtı taşıyabilir.
- Cut-off raporu received-not-invoiced, invoiced-not-received, rejected/inspection pending ve unmatched invoice kalemlerini aging ile gösterir.

## 13. Ek maliyet ve ödeme kontrolü

LandedCostAllocation navlun/sigorta/gümrük benzeri maliyeti miktar, ağırlık, hacim veya değer basis’iyle receipt/cost layer’a dağıtır; elle maliyet overwrite edilmez. Tedarikçi faturası, dağıtım ve stok değerleme/GL zinciri izlenir.

Ödeme önerisi yalnız approved ve hold’suz due schedule’lardan oluşur. Proposal hazırlayan, payment onaylayan ve banka gönderimini yapan roller risk/tutar eşiğine göre ayrılır. Tedarikçi banka hesabı değişikliği ödeme run’ından bağımsız doğrulama ve cooling-off/exception policy ister.

Ek testler: kısmi kabul + tek fatura, tek kabul + iki fatura, fiyat/miktar farkı, kalite reddi, GRNI kapanışı, landed cost ve duplicate supplier invoice.
