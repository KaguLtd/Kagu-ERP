# Fatura maliyeti → gerçek stok kabul hareketi doğrulaması

- MP-04 / INV-COST-008, R3, validating; tarih 13 Eylül 2026.
- Amaç: Fatura allocation'ını persisted physical receipt hareketiyle exact eşleştirmek.
- Ready: 0043 immutable stock movement ve invoice source reconciliation mevcut; yeni muhasebe politikası seçilmez.
- Okunanlar: MP-04 master, AGENTS, Inventory/Purchasing sözleşmeleri, 0043 source/position invariants, invoice preview ve mutabakat planı.
- Sahip: Teknik Codex; ürün/muhasebe atanmadı.

## Tasarım/done when

Explicit allocation→movement ID + expected receipt version eşlemesi 1–500 kaynak satırının tamamını kapsar. Inventory-owned okuyucu cost-publish/company/warehouse kapsamıyla persisted receipt kind=1, source type `purchasing.goods-receipt`, purpose `receipt`, source receipt/line/version, item/depo/UOM, recorded cutoff ve toplam bağlı miktarı doğrular. Source type/purpose yeni teknik adlandırmadır; mal kabulü writer'ı bu sözleşmeyi kullanmadan üretim yolu açılmaz. Reversal olarak üretilmiş veya cutoff içinde terslenmiş receipt kabul edilmez; düzeltme/iadeye yeni valuation akışı gerekir.

Bu kontrol diğer faturaların kullandığı miktarı hesaplamaz: PUR-COST-002 bağımsız kapasite ve gelecekteki atomik consumption writer ayrıca gerekir. Receipt effective date çıktıda korunur; invoice date'e zorla eşitlenmez, backdate/dönem onayı verilmez. Mevcut ham input preview korunur; yeni verified preview outer savepoint ile audit+receipt doğrulamasını atomik yapar. Yeni stock/GL kaydı, migration, grant, API yok. Kaynak hatası sıfır maliyet sayılmaz.

Testler: Persisted receipt positive, eksik hareket, yanlış version/item/depo, miktar aşımı, cutoff, tersleme ve audit rollback. Runtime MP-04 paketinde bekler; compile mali/DB kanıtı değildir. Commit/push yok.

## İlerleme

### 13 Eylül devam — toplu okuma tutarlılığı

INV-COST-008 R3 devamı: ReadCommitted altında her receipt için ayrı SELECT farklı statement snapshot'ları görebiliyordu. Amaç tüm hareket/reversal kanıtını tek bounded SELECT ile okumak ve sonra exact allocation gruplarını doğrulamak. Bu bir posting lock veya eşzamanlı source consumption çözümü değildir; aynı sorgu anındaki read-only kanıtı tutarlı yapar. Done when: tek batch sorgu, en fazla 500 ID, toplu kapasite/eksik ikinci receipt testleri, derleme ve doküman. Yeni migration/API/financial policy yok; mevcut kullanıcı değişiklikleri korunur.

Uygulandı: hareket ID dizisiyle tek SELECT ve immutable receipt evidence bellekte eşlemesi. Test fixture'ı iki invoice-line'ın aynı receipt kapasitesini aşması, eksik ikinci receipt/audit rollback ve ters sıra input'unda iki gerçek receipt sonucunun kaynak sırasını korumasıyla genişletildi. `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release --no-restore -v:q`: 36,28 sn, sıfır uyarı/hata. `git diff --check` içerik hatası yok; mevcut lock dosyalarında CRLF/LF uyarısı var. Runtime/DB senaryoları MP-04 toplu kapısına bırakıldı; compile eşzamanlılık/RLS/finansal kanıt olarak sunulmaz. Commit/push yok.

Inventory-owned verifier ve Bootstrap LoadReceiptVerifiedAsync eklendi. Owner fixture içinde gerçek stock receipt insert→fatura input→unit cost 0,13 ve receipt lineage senaryosu hazırlandı; eksik movement, yanlış allocation/version, miktar aşımı, receipt kayıt kesitinden önce okuma, append-only reversal sonrası ret ve audit rollback negatifleri eklendi. Fixture source sentetiktir; gerçek invoice SQL producer kanıtı değildir. Kapsam yetkisi okuma sonrası yeniden doğrulanır. Caller-owned fixture savepoint tüm hareketleri/audit'i geri alır; runtime henüz çalıştırılmadı.

Son cutoff/reversal senaryolarından sonra `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release --no-restore -v:q`: 21,14 sn, sıfır uyarı/hata. `git diff --check` içerik hatası yok; mevcut lock dosyalarında CRLF/LF uyarısı var. Runtime/DB/RLS/concurrency ve mali golden kapısı MP-04 paketinde bekler. Yeni migration/grant/API yok; commit/push yok. Gerçek fatura producer, kaynak consumption, period/valuation ve stock/GL kesinleştirme hâlâ açık.
