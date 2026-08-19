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
