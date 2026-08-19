# Performans ve Kapasite Planı

## 1. Amaç ve başlangıç profili

Tek Linux sunucuda çalışan orta ölçekli firma için ölçülebilir hizmet hedefleri ve büyüme eşikleri tanımlar. Tahmini rakamlar ölçüm yerine geçmez; pilot öncesi gerçek firma hacmiyle kapasite formu doldurulur.

Başlangıç test profili:

- 50–150 adlandırılmış kullanıcı,
- normalde 20–60, zirvede 100 eşzamanlı oturum,
- 100 bin cari, 100 bin stok kartı üst sınır testi,
- yılda 1–5 milyon iş belgesi/satır hacmi senaryosu,
- banka/e-fatura worker ve rapor yüküyle birlikte,
- en az beş yıllık sıcak muhasebe verisi.

Bu profil doğrulanana kadar donanım kararı kesin değildir.

## 2. SLO ve kullanıcı bütçeleri

Normal yük ve sunucu sağlıklıyken:

| İşlem | Hedef |
|---|---|
| Basit API okuma | p95 ≤ 500 ms |
| Filtreli liste ilk sayfa | p95 ≤ 2 s |
| Belge kaydetme | p95 ≤ 1,5 s |
| Finansal postala | p95 ≤ 3 s, uzun işse kuyruk |
| Dashboard özet | p95 ≤ 2 s veya güncellik etiketi |
| Kullanıcı arama önerisi | p95 ≤ 400 ms |
| Hata oranı | aylık başarılı isteklerde < %0,5 hedef |
| Kullanılabilirlik | iş saatinde başlangıç hedefi %99,5 |

Paket dışı büyük export, kapanış/değerleme ve toplu import asenkron iş olur; progress ve süre tahmini sunar.

## 3. Kapasite ölçüleri

- CPU kullanımı/throttling/load,
- RAM, swap, container OOM,
- disk kapasite, IOPS, latency, inode,
- PostgreSQL connection, lock/wait, cache hit, WAL, vacuum lag, slow query,
- API request rate/latency/error ve thread/GC,
- outbox kuyruk yaşı/deneme/dead-letter,
- blob throughput ve backup penceresi,
- istemci Web Vitals, Android start/crash/ANR.

Alarm yalnız ham CPU değil kullanıcı SLO ve tükenme hızıyla ilişkilendirilir.

## 4. Başlangıç donanım önerisi

Pilot için ayrı staging olduğu varsayımıyla üretim hostu başlangıç adayı:

- 8–16 modern vCPU,
- 32–64 GB ECC RAM,
- yansıtılmış enterprise NVMe/SSD,
- kullanılabilir kapasitenin en az %30'u boş,
- bağımsız ve şifreli uzak yedek hedefi,
- UPS ve izlenen disk/SMART,
- güvenilir 1 Gbps ağ, uygun internet yedekliliği.

DB + uygulama aynı hostta olduğundan RAM'in önemli kısmı PostgreSQL/OS cache'e ayrılır. Compose limitleri ölçümle ayarlanır. Yedek hedefi aynı disk/host değildir.

## 5. Veritabanı tasarımı

- Her sorgu company/scope ve seçici filtreyle başlar; buna uygun bileşik indeks.
- Para/tarih/numara doğru tip; gereksiz JSON ve `SELECT *` yok.
- Ana tablolar büyüme eşiğinde tarih/şirket partition değerlendirmesi; erken karmaşıklık yok.
- `EXPLAIN (ANALYZE, BUFFERS)` staging temsili hacimde incelenir.
- Uzun rapor primary transaction'ları bloke etmez; statement timeout ve ayrı read role/pool.
- Autovacuum ve statistics tablo hacmine göre izlenir; vacuum kapatılmaz.
- Connection pool toplamı PostgreSQL sınırını aşmayacak bütçelenir; servis başına sınırsız pool yok.
- N+1 ve offset'in derin sayfalama maliyeti; uygun yerde keyset/cursor.

## 6. Cache yaklaşımı

İlk sürümde Redis zorunlu değildir. Process içi kısa cache yalnız nadiren değişen, kullanıcı kapsamı doğru anahtarlanan referans veride kullanılabilir. Finansal bakiye ve yetki kararı bayat cache'e emanet edilmez. Çok instance ve dağıtık invalidation ihtiyacı ölçülürse ADR ile Redis eklenir.

## 7. Arka plan işleri ve backpressure

- İş sınıfına göre ayrı kuyruk/worker concurrency: e-fatura, bildirim, rapor, import.
- DB/sağlayıcı kapasitesini aşmayan limit; sınırsız paralellik yok.
- Kuyruk yaşı ve maksimum retry alarmı.
- Büyük import parça işler ancak iş atomikliği/staging sonucu korunur.
- Sağlayıcı kesintisinde circuit breaker; kullanıcı online işlemi worker beklemez.
- Disk/DB baskısında düşük öncelikli rapor/export yavaşlatılır.

## 8. Yük test senaryoları

1. Sabah giriş ve dashboard dalgası.
2. Satış siparişi/fatura + stok rezervasyon karışımı.
3. Depoda paralel mal kabul ve sayım.
4. 20 kullanıcı banka mutabakatı.
5. Muhasebe postala + mizan/defter sorguları.
6. Dönem sonu toplu değerleme/kapanış.
7. 100 bin satırlık import/export.
8. E-fatura sağlayıcı yavaşlığı ve biriken outbox.
9. Backup çalışırken normal operasyon.
10. Android senkronizasyon dalgası.

Warm-up, sabit yük, spike, soak (en az 8 saat) ve kapasite kırılma testi ayrılır. k6 benzeri scriptler sürüm kontrolündedir.

## 9. Büyüme ve ayrıştırma eşikleri

Önce sorgu/indeks/pool ve iş davranışı düzeltilir. Aşağıdakiler kalıcıysa sırasıyla ölçek ADR'si değerlendirilir:

- API CPU sınırı ve stateless yatay ölçek ihtiyacı,
- raporların primary DB SLO'sunu bozması → read replica/analitik store,
- worker işinin web latency'sini bozması → ayrı worker host,
- blob/backup I/O yarışması → ayrı object storage,
- tek host kullanılabilirlik hedefini karşılamıyor → HA topology.

Kubernetes, mikroservis veya cache performans sorununun varsayılan çözümü değildir.

## 10. Kabul

- Test veri hacmi ve script commit'i raporda yazılı.
- SLO'lar hedef eşzamanlılıkta sağlanmış ve kaynakta güvenli pay kalmış.
- Soak testte bellek/connection/queue sızıntısı yok.
- Backup ve ağır rapor altında iş işlemleri kabul sınırında.
- En pahalı 20 sorgu planı ve indeks gerekçesi kayıtlı.
- Kapasite alarm/eşik ve 12 aylık büyüme tahmini onaylı.

## 11. Defter ve kapanış iş yükü

Kapasite modeli yalnız günlük CRUD TPS’e dayanmaz. Ayrı ölçülür:

- peak posting: invoice + stock/cari/tax/GL + outbox tek transaction;
- payment allocation ve bank statement bulk match;
- backdated inventory valuation impact/repost;
- month-end control-account reconciliation ve report pack;
- as-of aging/GL drill-down ve concurrent export;
- projection rebuild ve restore sonrası full reconciliation.

Yük testi sentetik fakat dağılımı gerçekçi data skew taşır: az sayıda çok hareketli party/item/account, çok sayıda düşük hareketli kayıt, çoklu company/currency ve 5+ yıllık ledger.

## 12. Balance snapshot ve read model politikası

Mutable balance cache otorite değildir. Snapshot:

- source watermark/as-of;
- projection generation;
- company/account/item/party/dimension key;
- opening/movement/closing control total;
- rebuilt/validated time

taşır. Cache miss veya corruption ledger’dan rebuild edilir. Invalid generation rapora karışmaz.

Partition/indeks kararı effective_date ve company erişim desenine dayanır. Backdated event tüm tarihi kör taratmamalı; etkilenen dependency range/sequence indekslenir. Journal ve stock ledger write amplification, vacuum/WAL/backup maliyetiyle birlikte ölçülür.

## 13. Ek performans kabul hedefleri

Mutlak hedef pilot datasıyla ayarlanır; en az:

- ay kapanış mutabakat paketi iş penceresi içinde;
- 100 bin satırlık statement importu restartable ve UI’yi bloklamadan;
- 1 milyon satırlık GL/stock exportu streaming ve bounded memory ile;
- backdate impact preview tahmini satır/kapsamı göstererek timeout olmadan;
- projection rebuild sırasında normal read/write için tanımlı degradation bütçesi;
- restore sonrası integrity/reconciliation RTO içinde

kanıtlanır. Rapor hızlandırmak için yetki veya as-of bütünlüğü gevşetilmez.
