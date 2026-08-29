# Raporlama ve Gösterge Paneli Modülü

## 1. Amaç ve ilk sürüm yaklaşımı

Operasyonel kararlar ve mali kontrol için güvenilir, yetkili, veri kesim zamanı belli raporlar üretir. İlk sürümde ayrı veri ambarı kurulmaz; PostgreSQL okuma modelleri, materialized view ve arka plan özet tabloları kullanılır. Analitik yük veya tarihsel hacim bunu gerektirirse ADR ile veri ambarına geçilir.

## 2. Rapor sınıfları

- Operasyonel: açık sipariş, düşük stok, geciken tahsilat, onay kuyruğu.
- Finansal: mizan, bilanço, gelir tablosu, nakit pozisyonu, yaşlandırma.
- Uyum: KDV, e-fatura durum/istisna, dönem kapanış kontrolleri.
- Yönetim: satış/kârlılık, stok devir, tedarikçi performansı, nakit tahmini.
- Denetim: değişiklik/erişim/istisna/manuel kayıt raporları.

Mali raporların hesaplama tanımı sürümlenir ve mali müşavirce doğrulanır. Yönetim KPI'ları aynı adla farklı formül kullanamaz.

## 3. Rapor sözleşmesi

Her rapor şunları açıklar:

- benzersiz kod, isim, sahip ve amaç,
- ölçü ve boyut tanımları,
- veri kaynakları ve hariç tutmalar,
- `as_of`/veri kesim zamanı ve saat dilimi,
- para birimi/kur yöntemi,
- yenilenme sıklığı ve kabul edilen gecikme,
- erişim sınıfı ve PII alanları,
- toplamdan kaynağa drill-down yolu,
- örnek veri ve beklenen sonuç testi.

## 4. Okuma mimarisi

- Transactional raporlar uygun indeksli, salt okunur sorgu hizmetinden gelir.
- Ağır özetler background job ile atomik olarak yenilenir.
- Rapor sorgusu yazma transaction'ını uzun süre kilitlemez.
- Şirket/şube kapsamı hem uygulama filtresi hem uygun tablolarda RLS ile uygulanır.
- Cache anahtarı kullanıcı kapsamı, filtre, dil ve veri sürümünü içerir; hassas rapor ortak cache'e sızmaz.
- Büyük dışa aktarım asenkron iş olur; süreli güvenli indirme bağlantısı üretir.

## 5. Gösterge paneli UX'i

- En fazla 5–7 temel gösterge; kart kalabalığı yoktur.
- Her değerde dönem, para birimi ve güncellenme zamanı görünür.
- Renk tek başına anlam taşımaz; metin/ikon bulunur.
- Grafik, ayrıntı tabloya ve kaynak belgeye inebilir.
- “Canlı” ile “son yenileme” verisi karıştırılmaz.
- Kullanıcı rolüne göre varsayılan panel bulunur; yetkiyi aşan kişiselleştirme yapılamaz.

## 6. API ve dışa aktarma

- `GET /api/v1/reports/catalog`
- `POST /api/v1/reports/{code}/queries`
- `POST /api/v1/report-exports`
- `GET /api/v1/report-exports/{id}`
- `GET /api/v1/dashboards/{code}`

CSV/XLSX çıktısında formül enjeksiyonu önlenir; hücreler güvenli kaçar. PDF/çıktı kullanıcı, üretim zamanı, şirket, filtre ve gizlilik filigranı taşır. Maksimum satır ve süre limiti vardır.

## 7. Değişmez kurallar

- `RPT-INV-001`: Rapor, veri kesim zamanı ve kapsam olmadan sunulamaz.
- `RPT-INV-002`: Toplamdan kaynak satıra iniş aynı kapsam/filtreyi korur.
- `RPT-INV-003`: Finansal rapor toplamları aynı dönem mizanınla mutabık olmalıdır.
- `RPT-INV-004`: Yetkisiz sütun dışa aktarmada da görünmez.
- `RPT-INV-005`: Yenileme başarısızsa eski veri “güncel” etiketiyle gösterilmez.
- `RPT-INV-006`: Rapor tanımı değişikliği sürüm ve regresyon testi ister.
- `RPT-CTRL-001`: Alt defter ve GL kontrol hesabı snapshot'ları yalnız aynı tenant, company, currency, effective-date as-of, veri kesimi, projection generation ve boyut kesiminde karşılaştırılabilir.
- `RPT-CTRL-002`: Her kontrol snapshot'ında `opening + debits - credits = closing` olmalı; mutabakat farkı `subledger closing - GL closing` olarak exact decimal hesaplanır ve sessiz tolerans uygulanmaz.
- `RPT-PARTY-001`: Cari ekstre satırları aynı tenant/company/party account/control account/currency kesiminde effective as-of ve recorded-at veri kesimiyle deterministik sıralanır; kapanış bakiyesi immutable olay etkilerinden türetilir.
- `RPT-PARTY-002`: Aging yalnız aynı rapor kesimindeki açık vade kalemlerini sürümlü ve açık bucket policy snapshot'ıyla sınıflar; bucket toplamları, aging toplamı ve aynı kesimdeki cari ekstre kapanışı exact decimal cross-foot eder.

## 8. Performans ve test

- En yaygın interaktif liste/özet p95 hedefi normal yükte 2 saniyenin altıdır.
- Büyük raporlar kuyruklanır; API zaman aşımını tüketmez.
- Açıklama planları temsilî hacimde izlenir; N+1 sorgusu engellenir.
- Altın veri setiyle KPI, mali toplam, tarih/kur ve yetki testleri yapılır.
- Aynı anda rapor + muhasebe postalamada kilit/bekleme testi yapılır.
- Excel/CSV enjeksiyon ve büyük veri bellek testleri zorunludur.

## 9. Zorunlu rapor kataloğu

### Finans ve muhasebe

- trial balance; GL detail; journal ve source-to-GL audit trail;
- balance sheet, P&L ve cash-flow mapping/workpaper;
- AR/AP aging ve party statement, taksit/open-item bazlı;
- bank/cash book, transit/outstanding payment ve reconciliation;
- cheque/promissory-note portfolio, maturity ve custody;
- tax sales/purchase journal, control account ve filed-return changes;
- manual journal, period reopen/repost ve posting exception.

### Ticaret ve stok

- sales/purchase order remaining; dispatch/receipt/invoice linkage;
- received-not-invoiced, invoiced-not-received, uninvoiced dispatch ve goods in transit;
- inventory ledger, on-hand/reserved/available, valuation ve movement;
- lot/serial trace, expiry, count variance/recount ve negative-stock exception;
- gross margin; kullanıcının cost permission’ı yoksa kaynak ve export’ta da maskeli.

Her rapor requirement ID, owner, accounting meaning, grain, measures, dimensions, filters, currency/rate policy, effective-date/as-of, generation, total/cross-foot, drill-down, permission ve export retention sözleşmesi taşır.

## 10. Rapor mutabakat zinciri

Rapor toplamı materialized view veya cache’e güvenerek tek başına kabul edilmez:

source documents ↔ subledger entries ↔ control account GL ↔ financial statement line.

Cross-foot kontrolleri aynı snapshot/watermark üzerinde çalışır. Bir kullanıcı raporu açarken concurrent posting olursa tüm sayfalar aynı as-of token’ı kullanır veya “veri değişti” diyerek yeniden başlatır; sayfalar farklı kesimlerden birleşmez.

Comparative columns current/prior period, budget varsa actual/budget ve transaction/functional currency semantiğini açık etiketler. Sıfır, null, not-applicable ve veri gecikmesi farklı gösterilir.

## 11. Drill-down, export ve performans

Financial statement line → hesap → journal line → source event/document → approval/tax/attachment zinciri permission korunarak açılır. Her adım filter/as-of context’i taşır; yetkisiz satır sayısı veya tutarı yan kanalla sızmaz.

Export manifesti report version, filters, sort, timezone, as-of/watermark, projection generation, row count, control totals, generated-by/at ve file hash içerir. CSV injection, locale decimal/date, büyük dosya streaming ve download audit zorunludur.

Golden report paketi trial balance, alt defter/GL, aging, bank closing, stock valuation ve mali tabloları aynı veri setinde bağımsız oracle ile cross-foot eder.

## 12. Uygulama kanıtı — projection generation manifesti

`reporting.projection_generation` ve dimension alt tablosu, `RPT-INV-001`, `RPT-INV-005` ve `RPT-INV-006` için rapor kesimi ile source lineage'ını append-only kaydeder. Aynı generation kimliğinin farklı checksum veya metadata ile yeniden kullanımı reddedilir; dimension sayısı deferred DB guard ile commit anında tam eşleşir. Uygulama rolü yalnız scoped `SELECT`/`INSERT` yetkisine sahiptir ve forced RLS şirketler arası görünürlüğü engeller.

Bu manifest rapor rakamlarının kaynağı değildir, source modül tablolarını okumaz ve projection generation işini kendi başına tamamlamaz. Rapor tanımı, account mapping, aging policy, job orchestration ve permission sözleşmeleri ilgili sahiplerce ayrıca karara bağlanacaktır. Teknik kanıt [projection generation manifest planında](../project/plans/2026-08-26-report-projection-generation-manifest-spike.md) kayıtlıdır.

Manifestin authoritative okuma yolu, header ve dimension kesimini aynı transaction/company scope içinde domain modeline yeniden kurar. Persisted metadata ve lineage birebir doğrulanır; missing veya cross-company lookup fail-closed kalır. Kanıt [authoritative loader planında](../project/plans/2026-08-26-authoritative-report-projection-generation-loader-spike.md) kayıtlıdır.

Doğrulanmış cari ekstre projection'ı, generation manifestine immutable header ve normalize olay satırlarıyla bağlanır. DB guard satır sayısı, running exposure ve closing exposure değerlerini exact cross-foot eder; aynı statement ID'nin farklı payload ile kullanımı reddedilir. Bu persistence source-to-sign veya opening balance politikası seçmez ve Parties tablolarını doğrudan okumaz. Kanıt [party statement projection planında](../project/plans/2026-08-26-party-statement-projection-persistence-spike.md) kayıtlıdır.

Cari ekstre projection'ının authoritative loader'ı manifest, header ve deterministik satırları tek transaction/company scope içinde yeniden kurar ve Reporting domain invariantlarını tekrar uygular. Missing veya cross-company lookup fail-closed kalır; loader source modül tablolarını okumaz. Kanıt [authoritative statement loader planında](../project/plans/2026-08-26-authoritative-party-statement-projection-loader-spike.md) kayıtlıdır.

Generation sırasında kullanılan açık calendar-day aging policy kimliği, sürümü ve bucket aralıkları immutable snapshot olarak saklanır. Deferred DB guard bucket sayısı ile tüm integer-day aralığının kesintisiz ve çakışmasız kapsandığını doğrular. Bu kayıt tenant varsayılanı veya policy approval otoritesi değildir. Kanıt [aging policy snapshot planında](../project/plans/2026-08-26-aging-policy-projection-snapshot-spike.md) kayıtlıdır.

Aging policy snapshot loader'ı policy kimliği/sürümü ve ordinal bucket aralıklarını aynı transaction/company/generation scope içinde domain modeline yeniden kurar. Domain full-range ve contiguous coverage invariantlarını okuma sırasında tekrar uygular; cross-company lookup fail-closed kalır. Kanıt [authoritative aging policy loader planında](../project/plans/2026-08-26-authoritative-aging-policy-projection-loader-spike.md) kayıtlıdır.

Doğrulanmış aging item projection'ı aynı generation ve policy snapshot'ına immutable bağlanır. Deferred DB guard item count ile total remaining değerini exact cross-foot eder. Bucket summary ayrı bir otorite olarak saklanmaz; aynı policy ve item snapshot'larından domain tarafından yeniden türetilir. Kanıt [party aging projection planında](../project/plans/2026-08-26-party-aging-projection-persistence-spike.md) kayıtlıdır.

Aging projection loader'ı generation manifesti, policy snapshot, header ve item'ları tek transaction/company scope içinde domain modeline yeniden kurar. Total ve bucket summaries authoritative item/policy snapshot'larından yeniden hesaplanır; missing veya cross-company lookup fail-closed kalır. Kanıt [authoritative aging loader planında](../project/plans/2026-08-27-authoritative-party-aging-projection-loader-spike.md) kayıtlıdır.

Authoritative Party report composition, statement ve aging projection'larını aynı transaction/company scope içinde yükler; aynı report slice, hesap ve balance-side bağlamını doğrulayıp statement closing exposure ile aging total remaining değerini exact cross-foot eder. Missing veya cross-company birleşim fail-closed kalır. Kanıt [Party report cross-foot composition planında](../project/plans/2026-08-27-authoritative-party-report-cross-foot-composition-spike.md) kayıtlıdır.

Control-account balance projection'ı aynı generation için subledger ve GL snapshot'larını ledger-side bazında tekilleştirir; opening, debit, credit ve closing arithmetic DB ile exact korunur. Authoritative loader manifest kesimini yeniden kurar ve reconciliation composition yalnız aynı report slice/control account içindeki iki tarafı karşılaştırır. Kanıt [control-account projection planında](../project/plans/2026-08-27-control-account-balance-projection-persistence-spike.md) kayıtlıdır.

Party report query gate, resource lookup'tan önce company scope ve versioned report definition'ın verdiği required permission kodunu doğrular. Gate permission kodunu seçmez; eksik izin aynı typed denial ile fail-closed kalır ve rapor varlığını sızdırmaz. Kanıt [permission-first query gate planında](../project/plans/2026-08-27-permission-first-party-report-query-gate-spike.md) kayıtlıdır.

Audited Party report composition, trusted audit context'i execution scope ile eşleştirir ve allowed/denied sonucu ortak transaction-bound append-only audit writer'a iletir. Denied audit target kimliği taşımaz; audit yazılamazsa rapor sonucu dönmez. Reporting platform audit tablosuna doğrudan yazmaz ve uygulama rolüne audit okuma yetkisi verilmez. Kanıt [audited query composition planında](../project/plans/2026-08-27-audited-party-report-query-composition-spike.md) kayıtlıdır.

Atomic projection publisher, statement-aging ile subledger-GL cross-foot kontrollerini bütün write'lardan önce yapar ve manifest, policy, statement, aging ve iki ledger-side snapshot'ını tek caller-owned transaction içinde deterministik sırayla yazar. Tam replay yeni fact üretmez; invalid set zero-write fail-closed kalır. Kanıt [atomic publication planında](../project/plans/2026-08-27-atomic-party-report-projection-publication-spike.md) kayıtlıdır.

Reporting, Party source tablolarını doğrudan okumaz; bağımsız `Parties.Contracts` yüzeyinden explicit scope/as-of/cutoff, opening, watermark/checksum ve open-item/impact fact'lerini alır. Restriction evidence bulunmuyorsa `Unavailable` taşınır; aging projection bunu sessizce clear kabul edemez. Contract kanıtı [Party report source contract planında](../project/plans/2026-08-27-party-report-source-contract-spike.md) kayıtlıdır.

Source batch'ten statement ve aging modeli üreten application builder, açık kalem başlangıç olayının source type/effective/recorded kanıtlarını kullanır; due date'i işlem tarihi saymaz. Event işaretleri normalize edilir, statement closing ile source remaining toplamı exact cross-foot edilir ve `Unavailable` restriction kanıtıyla aging fail-closed kalır. Kanıt [projection builder planında](../project/plans/2026-08-27-party-report-projection-builder-spike.md) kayıtlıdır.

Party restriction source güncellemesi (28 Ağustos 2026): Authoritative Party adapter, append-only dispute/collection-block applied/released akışını aynı effective-as-of ve recorded-cutoff kesitinde yükler. Hiç olay olmaması artık kanıtlı `Clear` sayılır; aktif kombinasyon explicit `Disputed`, `Blocked` veya `DisputedAndBlocked` üretir. Aging bu kalemleri toplam bakiyeden çıkarmaz; mevcut is-disputed/is-blocked alanlarıyla ayrı subtotal/filtreye taşır.

Atomic publication'a gidecek statement ve aging ayrı report code'larıyla üretilemez. Pair builder ikisini aynı versioned report definition ve projection generation kesiminde üretir; `PartyStatementAgingCrossFoot` dönüşten önce çalışır ve uyumsuz çift job/publisher sınırına ulaşamaz.

Projection application job'u source, aging policy, control-account evidence ve publication bağımlılıklarını portlarla ayırır. Source query scope/cut eşleşmesi ile balance evidence control-account/report-slice eşleşmesi publish öncesi zorunludur; eksik veya mismatched evidence zero-publish fail-closed kalır. Scheduler ve transaction sahipliği bu application katmanının dışında tutulur. Kanıt [job orchestration planında](../project/plans/2026-08-27-party-report-projection-job-orchestration-spike.md) kayıtlıdır.

PostgreSQL projection sink adapter'ı trusted execution scope'u transaction-local RLS context'e aktarır ve mevcut atomic publisher'ı tek connection/transaction içinde çalıştırır. Bütün immutable set başarıyla yazılmadan commit edilmez; tam replay yeni fact üretmez ve cross-company scope bağlantı yazımından önce reddedilir. Gerçek DB kanıtı [sink planında](../project/plans/2026-08-27-postgres-party-report-projection-sink-spike.md) kayıtlıdır.

Atomic publisher ayrıca statement/aging Party control account kimliği ile hem subledger hem GL snapshot control account kimliğini write öncesi eşleştirir. Birbiriyle uzlaşan fakat başka hesaba ait balance çifti publication setine dahil edilemez.

Sink job katmanının atlanabileceğini varsayarak publication command ile source batch ve doğrulanmış pair arasındaki tenant/company/party/control-account/currency/as-of/cutoff/report-definition/generation bağını connection açmadan yeniden doğrular. Değiştirilmiş bağlam `PARTY_REPORT_PUBLICATION_CONTEXT_MISMATCH` ile fail-closed kalır.

Statement drill-down ilk olarak Reporting-owned immutable projection satırını exact company, projection generation, statement ve event kimlikleriyle authoritative anchor'a dönüştürür. Anchor aynı report slice'ı, normalized source type/event, due-schedule line, varsa payment ve running exposure kanıtını korur; yanlış generation veya cross-company lookup kaynak satır varlığını göstermez. Kanıt [drill-down anchor planında](../project/plans/2026-08-27-party-statement-drill-down-anchor-spike.md) kayıtlıdır.

Party source checksum dışarıdan verilen biçimsel bir değer değildir; scope, effective/recorded kesimleri, opening, watermark ve canonical sıralı fact payload'ından contract tarafından SHA-256 olarak üretilir. Equivalent replay aynı hash'i üretir, lineage veya fact değişikliği hash'i değiştirir ve contract iç koleksiyonları defensive copy ile korur.
