# Çek ve Senet Modülü

## 1. Amaç ve sınır

Müşteriden alınan ve firmaca verilen çek/senetlerin fiziksel veya elektronik yaşam döngüsünü, vade riskini, cirosunu, tahsil/ödeme sonucunu ve muhasebe bağlantısını izler. Yazılım hukuki geçerlilik kararı vermez; güncel KKTC mevzuatı, banka uygulamaları ve mali müşavir/hukukçu kararıyla tanımlanan politikayı uygular.

## 2. Araç kimliği ve varlıklar

`negotiable_instrument` en az şu alanları taşır:

- tür: alınan/verilen çek, alınan/verilen senet,
- banka/şube/hesap ve seri numarası (uygunsa),
- keşideci, lehtar, cari hesap,
- düzenleme ve vade tarihi,
- tutar, para birimi ve kur anlık görüntüsü,
- fiziki konum/sorumlu,
- risk sahibi ve teminat bilgisi,
- hassas veri sınıfı.

Bağlı varlıklar: `instrument_event`, `endorsement`, `custody_transfer`, `collection_instruction`, `dishonour_record`, `instrument_link`, `instrument_accounting_link`.

## 3. Olay tabanlı durum modeli

Durum, değiştirilebilir tek bir metin yerine doğrulanmış olaylardan türetilir. Olası olaylar:

- `received` / `issued`
- `placed_in_safe`
- `endorsed`
- `sent_for_collection`
- `pledged_as_collateral`
- `collected` / `paid`
- `dishonoured`
- `returned`
- `cancelled`
- `replaced`

Her olay tarih-saat, işlem tarihi, kullanıcı, fiziksel konum, karşı taraf, kanıt belge, gerekçe ve önceki olay hash'ini taşır. Geçersiz geçişler sunucu tarafında reddedilir.

## 4. Değişmez kurallar

- `CHK-INV-001`: Banka/hesap/seri veya tanımlı alternatif kimlik birleşimi şirket içinde tekildir.
- `CHK-INV-002`: Tahsil edilmiş/ödenmiş araç tekrar ciro veya tahsile gönderilemez.
- `CHK-INV-003`: Fiziksel teslim ve teslim alma, iki taraflı kullanıcı/zaman kaydı olmadan tamamlanmış sayılmaz.
- `CHK-INV-004`: Ciro zinciri kopamaz; her ciro önceki lehtar ve yeni lehtarı belirtir.
- `CHK-INV-005`: Karşılıksız sonucu, önceki tahsil beklentisini tersler; kaynak kayıt silinmez.
- `CHK-INV-006`: Vade, tutar ve taraf gibi esas alanlar ilk onaydan sonra değiştirilemez; iptal/değiştirme olayı gerekir.
- `CHK-INV-007`: Araç riski cari risk raporunda yalnızca bir kez sayılır; ciro/teminat politikası açıkça belirlenir.

## 5. Süreçler

### 5.1 Alınan çek/senet

Kayıt → çift kontrol → kasa teslimi → bekleyen portföy → bankaya tahsil/ciro/teminat → sonuç → cari ve muhasebe kapama.

### 5.2 Verilen çek/senet

Ödeme önerisi → yetki/limit onayı → seri seçimi → teslim tutanağı → vade takibi → banka sonucu → borç kapama.

### 5.3 Karşılıksız/ödenmeme

Kanıt belgesi eklenir, olay kaydedilir, önceki tahsil/ödeme kaydı terslenir, cari risk geri açılır, kullanıcı uyarıları ve hukuki takip görevi oluşturulur. Gecikme/masraf hesapları yürürlük tarihli şirket politikasıdır.

## 6. Muhasebe politikası

Alınan/verilen çek portföyü, tahsildeki çek, ciro edilen çek, teminat, tahsil/ödeme ve karşılıksız durumlar için hesap şablonları sürümlenir. Ciro edilen çekin müşteri riskini azaltıp azaltmayacağı ayrı bir risk politikasıdır; kod içine gömülmez.

Her muhasebe fişi kaynak araç ve olay kimliğine bağlanır. Aynı olayı yeniden işlemek ikinci fiş üretmez.

## 7. Yetki ve güvenlik

- Kayıt, fiziki teslim, onay ve muhasebeleştirme görevleri ayrılabilir.
- Seri numarası ve banka bilgisi listelerde maskelenebilir.
- Yüksek tutarlı araçta çift onay ve MFA istenir.
- Fiziksel konum değişikliği teslim tutanağı ve mümkünse barkod/QR taraması ister.
- Silme yoktur; yanlış kayıt gerekçeli iptal edilir.
- Dışa aktarımlar filigran, kullanıcı ve zaman damgası taşır.

## 8. API, ekranlar ve raporlar

- `POST /api/v1/instruments`
- `POST /api/v1/instruments/{id}/events`
- `POST /api/v1/instruments/{id}/endorsements`
- `POST /api/v1/instruments/{id}/custody-transfers`
- `GET /api/v1/instruments/maturity-calendar`

Ekranlar: portföy panosu, vade takvimi, araç detayı/zaman çizelgesi, hızlı teslim, tahsilat dosyası, karşılıksız işlem kuyruğu.

Raporlar: vade dağılımı, portföy/ciro/teminat, karşı taraf riski, karşılıksız oranı, fiziki konum, yaklaşan vadeler ve muhasebe mutabakatı.

## 9. Kabul testleri

- Her durum için izinli/yasak geçiş matrisi.
- Yinelenen seri numarasının eşzamanlı kayıtta engellenmesi.
- Ciro zinciri ve risk toplamının örnek senaryolarla doğrulanması.
- Tahsil sonrası karşılıksız olayının cari/muhasebe terslemesi.
- Fiziksel teslimde iki taraflı kanıt zorunluluğu.
- Yetkisiz şirket/şube/maskeleme erişimi.
- Vade uyarılarının saat dilimi ve tatil takviminde doğru üretilmesi.

## 10. Hukuki doğrulama kapısı

Canlıya çıkmadan önce çek/senet alanları, ciro/ibraz/karşılıksız süreçleri ve saklama süreleri KKTC Merkez Bankası mevzuat dizini, ilgili bankalar ve yetkili hukuk/mali müşavirle doğrulanıp [hukuki matrise](../legal/01-kktc-legal-matrix.md) işlenmelidir.

## 11. Araç, custody, allocation ve settlement ayrımı

NegotiableInstrument ekonomik aracı; InstrumentEvent onun custody/risk durumunu; PaymentAllocation hangi cari açık kaleme uygulandığını; BankSettlement bankada tahsil/ödeme sonucunu gösterir. Bu dört bilgi tek status alanında birleşmez.

- received/issued olayı araç nominali, currency, drawer/drawee/payee ve maturity’yi snapshot eder.
- custody location; kasa, banka tahsil, teminat, ciro edilen taraf veya iade alanı olarak ayrı izlenir.
- endorsement yeni hak/sorumluluk ve party bağlantısıdır; geçmiş owner/custodian satırı overwrite edilmez.
- bankaya verildi durumu tahsil edildi değildir; presentation, cleared, dishonoured ve returned olayları tarih/kanıt taşır.
- açık kaleme tahsis, aracın fiziksel/banka durumunu değiştirmez; dishonour allocation’ı karşı olayla açar ve gerekiyorsa masraf/risk hareketi üretir.

## 12. Cut-off, rapor ve iç kontrol

Maturity date, presentation date, effective accounting date ve recorded time ayrı tutulur. Dönem sonunda portföyde, bankada, teminatta, ciroda, vadesi geçmiş ve karşılıksız araçlar nominal/yerel tutar ve GL control account ile mutabık raporlanır.

Araç teslim alma, ciro/teminat kararı, bankaya ibraz ve write-off eşik bazlı farklı roller ister. Fiziksel envanter sayımı kör/çift kontrollü yapılır; kayıp araç security/incident ve legal hold sürecini tetikler.

Kabul senaryoları kısmi cari tahsis, ciro sonrası karşılıksız, bankadan dönüş, yenileme/yeni araç, kur farkı ve restore sonrası tam olay zincirini içerir. Hukuki sonuç ve muhasebe hesabı resmi/uzman onayı olmadan sabitlenmez.
