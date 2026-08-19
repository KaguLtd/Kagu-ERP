# 02 — Domain Sözlüğü

Kod, API, UI ve dokümanlarda aşağıdaki anlamlar kullanılır. Aynı kavram için yeni eş anlam üretme.

| Türkçe terim | Kod adı | Tanım |
|---|---|---|
| Tenant | `Tenant` | Birbirinden veri ve operasyon olarak bağımsız müşteri/organizasyon sınırı |
| Şirket | `Company` | Yasal defter, vergi, para ve dönem sahibi tüzel/işletme birimi |
| Şube | `Branch` | Şirket altındaki operasyon ve belge numarası bağlamı |
| Depo | `Warehouse` | Fiziksel stok sorumluluğu ve hareket bağlamı |
| Cari | `Party` / `PartyAccount` | Müşteri, tedarikçi veya her ikisi; ona ait alt defter hesabı |
| Açık kalem | `OpenItem` | Henüz tam kapatılmamış fatura/borç/alacak hareketi |
| Kapama | `Settlement` | Ödeme, tahsilat, mahsup veya kredi ile açık kalem arasındaki tutar bağlantısı |
| İşlem para birimi | `TransactionCurrency` | Belgenin düzenlendiği para |
| Fonksiyonel para | `FunctionalCurrency` | Şirketin yasal muhasebe para birimi |
| Raporlama parası | `ReportingCurrency` | Yönetim raporu için opsiyonel üçüncü para |
| Posting | `Post` / `Posting` | Onaylı ticari olayın değişmez alt defter ve muhasebe hareketine dönüşmesi |
| Fiş | `JournalEntry` | Dengeli muhasebe başlığı ve borç/alacak satırları |
| Ters kayıt | `Reversal` | Asıl kesin kaydı koruyarak etkisini karşı yönde sıfırlayan yeni kayıt |
| Dönem kilidi | `PeriodLock` | Belirli tarih aralığına yeni posting yapılmasını engelleyen kontrol |
| Fiziksel stok | `OnHandQuantity` | Depoda kesinleşmiş girişler eksi çıkışlar |
| Rezerve stok | `ReservedQuantity` | Onaylı talepler/siparişler için ayrılmış miktar |
| Kullanılabilir stok | `AvailableQuantity` | Fiziksel eksi rezerve ve bloke miktar |
| Beklenen stok | `ExpectedQuantity` | Açık satın alma/transfer kabulünden beklenen miktar |
| Maliyet katmanı | `CostLayer` | Stok değerleme yönteminin miktar ve birim maliyet parçası |
| Belge | `BusinessDocument` | Sipariş, sevk, fatura, kabul vb. kontrollü state machine sahibi kayıt |
| Kesinleşmiş | `Posted` / `Issued` | Finansal/operasyonel etkisi oluşmuş ve yerinde değiştirilemez durum |
| İdempotency | `IdempotencyKey` | Aynı iş isteğinin tekrarında yeni sonuç yerine aynı sonucu üretme garantisi |
| Outbox | `OutboxMessage` | İş transaction'ıyla birlikte kaydedilen, dışa sonra teslim edilen mesaj |
| Inbox | `InboxMessage` | Dış sistemden gelen olayın tekrar işlenmesini önleyen kayıt |
| Audit | `AuditEvent` | Kim, ne zaman, hangi kapsamda, hangi eylemi ve gerekçeyle yaptı kaydı |
| Scope | `DataScope` | Kullanıcının erişebildiği tenant/şirket/şube/depo/banka/maliyet merkezi kümesi |
| Permission | `Permission` | Bir domain eylemini yapabilme yetkisi, ör. `invoice.post` |
| Maker-checker | `SegregationOfDuties` | Hazırlayan ile onaylayanın ayrılması |
| Çek/senet | `NegotiableInstrument` | Alınan/verilen çek, senet veya poliçe benzeri kıymetli evrak |
| Ciro | `Endorsement` | Çek/senet üzerindeki hakkın başka tarafa devri olayı |
| Vergi kuralı | `TaxRule` | Etki tarihi, oran/istisna, öncelik ve yasal kaynak sahibi sürümlü kural |
| Vergi kararı | `TaxDecision` | Belge satırında uygulanan kural ve hesap sonucunun değişmez snapshot'ı |
| e-Fatura zarfı | `EInvoiceEnvelope` | Gönderilen kesin XML, hash, durum, yanıt ve retry bağlamı |
| Yasal arşiv | `LegalArchive` | Saklama/ibraz ve legal hold kurallarına tabi değişmez belge paketi |
| RPO | `RecoveryPointObjective` | Felakette kabul edilen azami veri kaybı zamanı |
| RTO | `RecoveryTimeObjective` | Hizmetin geri dönmesi için kabul edilen azami süre |

| Taahhüt | Commitment | Henüz ekonomik etkisi doğmamış sipariş, sözleşme veya rezervasyon yükümlülüğü |
| Ekonomik olay | EconomicEvent | Stok, hak, borç/alacak veya nakit gibi ekonomik kaynağı değiştiren gerçekleşmiş olay |
| Kaynak / aktör | Resource / Agent | REA dilinde olaydan etkilenen ekonomik değer ve olaya katılan iç/dış taraf |
| Alt defter | Subledger | Cari, stok, banka veya çek gibi ayrıntılı hareket defteri |
| Kontrol hesabı | ControlAccount | Alt defter toplamını GL’de temsil eden ve onunla mutabık olması gereken hesap |
| Ödeme | Payment | Banka/kasa/çek üzerinden değer transferi; tek başına hangi faturanın kapandığını söylemez |
| Tahsis / allocation | PaymentAllocation | Ödeme, kredi veya mahsup tutarının belirli açık kalem/vade dilimine bağlanması |
| Tahsis kaldırma | Unallocation | Kaynak ödeme ve GL etkisini silmeden kapama bağlantısını karşı olayla geri alma |
| Banka kesinleşmesi | BankSettlement | Banka ekstresi veya sağlayıcı kanıtıyla para hareketinin bankada gerçekleştiğinin doğrulanması |
| Taksit/vade dilimi | DueScheduleLine | Bir belgenin ayrı vade, tutar ve açık kalem olarak izlenen ödeme parçası |
| Etkin tarih | EffectiveDate | Ekonomik/mali etkinin ait olduğu tarih |
| Kayıt zamanı | RecordedAt | Olayın sisteme ilk kez kaydedildiği değişmez zaman |
| Posting zamanı | PostedAt | Alt defter/GL etkisinin kesinleştirildiği zaman |
| Yeniden posting | Repost | Kaynak olayı değiştirmeden bozuk/eski türetilmiş projection’ı kontrollü yeniden üretme |
| Cut-off | CutOff | Bir döneme ait olayların doğru ve tam döneme alınmasını sağlayan kapanış sınırı |
| GRNI / teslim alındı, faturalanmadı | GoodsReceivedNotInvoiced | Mal/hizmet alınmış fakat tedarikçi faturası henüz gelmemiş geçici/tahakkuk durumu |
| Yoldaki mal | GoodsInTransit | Sahiplik/ekonomik risk devretmiş ancak hedef depoya fiziksel kabulü tamamlanmamış stok |
| Kör sayım | BlindCount | Sayımı yapan kişiye beklenen sistem miktarının gösterilmediği sayım yöntemi |
| Projection generation | ProjectionGeneration | Türetilmiş defter/read model satırlarının hangi yeniden üretim çalışmasına ait olduğunu gösteren sürüm |
| Posting istisnası | PostingException | Kaynak olayın mali etkisinin üretilemediği, tam muhasebeleşmiş sayılmayan ve insan çözümü isteyen kayıt |

## Belge durum sözlüğü

- `draft`: Düzenlenebilir; finansal etkisi yok.
- `submitted`: Onaya gönderilmiş; kontrollü kilit.
- `approved`: İş kuralı/onay geçmiş; posting bekliyor.
- `posted`: Alt defter/GL etkisi oluşmuş; değişmez.
- `partially_settled` / `partially_fulfilled`: Kısmen kapandı/sevk oldu.
- `closed`: Süreç tamamlandı.
- `rejected`: Onay reddedildi; gerekçe zorunlu.
- `cancelled`: Taslak/onaylı fakat posting öncesi iptal veya mevzuat akışına uygun iptal.
- `reversed`: Posted kaydın etkisi ayrı karşı kayıtla geri alındı.

`deleted`, posted belge için geçerli bir business state değildir.
