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
