# MP-01 Firma Politikası Temel Seti ve MP-03 Geçiş Planı

- **Amaç:** 27 Ağustos 2026 tarihli kullanıcı kararlarını güvenli ürün politikalarına dönüştürmek ve aynı carinin müşteri/tedarikçi ile döviz bazlı ayrı hesaplarını taşıyan ilk MP-03 iş dilimini açmak.
- **Master fazı ve kapısı:** MP-01 karar kanıtı / MP-03 giriş kapısının kapsam bazlı yeniden değerlendirilmesi.
- **Risk sınıfı:** R4 — muhasebe, dönem, para, yetki ve ileride production davranışı.
- **Durum:** in-progress
- **Sahip:** KaguLtd repository sahibi; uzman ve isimli roller `DEC-MP01-019` gereği şimdilik `atanmadı`.
- **Başlangıç / hedef tarih:** 2026-08-27 / ilk PartyAccount diliminin doğrulanması.
- **İlgili requirement ID'leri:** ORG-POL-001, ORG-POL-002, PARTY-ACC-001, PARTY-ACC-002, PARTY-DUE-001, PARTY-OI-001, PARTY-OI-002, ACC-INV-002, ACC-INV-003, ACC-PER-001, IAM-POL-001, WFL-INV-003.
- **Etkilenen belgeler/modüller:** ORG, IAM, PARTY, GL, WF, RPT, migration ve integration testleri.
- **Okunan zorunlu belgeler:** `AGENTS.md`, `MASTER_PLAN.md`, `PLANS.md`, `docs/README.md`, veri mimarisi, ortak iş akışları, IAM/ORG/PARTY/GL/WF/RPT modül sözleşmeleri, hukuk matrisi, resmi açık sorular ve `src/Modules/Parties/AGENTS.md`.
- **Definition of Ready sonucu:** conditional pass. Bu plandaki şirket, dönem, kur, cari ve yetki davranışları ürün sahibi kararıyla geliştirmeye hazırdır. Resmi hesap eşlemeleri, banka tetikleyicileri, vergi ve production uygunluğu uzman/resmi kanıt bekler.

## Master plan ilişkisi

Bu çalışma `DEC-MP01-001`–`010`, `012` ve `021`–`024` için ürün politikası kanıtı oluşturur. `DEC-MP01-011` stok ve `013` sonrası resmi/production kararları açık kalır. Bu nedenle MP-01 tamamlanmaz; fakat MP-03'ün cari, banka ve muhasebe çekirdeği dilimleri `in-progress` olabilir.

## Kapsam

### Dahil

- Tek tenant altında yönetilebilir çok şirket; geleceğe açık şube ve zorunlu çok depo/proje altyapısı.
- Takvim yılı, aylık dönem modeli, kapalı dönem güvenlik yorumu.
- TRY fonksiyonel para; TRY/USD/EUR/GBP tek dövizli PartyAccount ve günlük manuel kur politikası.
- Para/kur hassasiyeti, yaşlandırma, allocation, fazla ödeme ve write-off politikası.
- Aynı Party altında müşteri ve tedarikçi PartyAccount ayrımı; opening event kanıtı tasarımı.
- Granüler permission ve altı başlangıç şablonu.

### Dahil değil

- 120/320 dışındaki resmi hesap kataloğunu veya posting mapping'lerini üretim için onaylamak.
- Banka reconciliation/transit hesaplarını, stok değerlemeyi, vergi/e-Fatura veya production kararlarını seçmek.
- Kesinleşmiş kayıtları yerinde değiştiren bir yol açmak.

## Değişmezler ve güvenlik sınırları

- Kesinleşmiş belge/fiş/cari kayıt yalnız linked reversal/correction ile düzeltilir.
- Kritik işlem hazırlayan tarafından onaylanamaz. Tek yönetici onayı gereken akışta yönetici hazırlayandan farklı kişidir; hard-close reopen iki farklı onay, gerekçe ve audit ister.
- Company fonksiyonel para birimi PartyAccount dövizinden ayrıdır. Başlangıç fonksiyonel para TRY'dir.
- Aynı Party kopyalanmaz; rol + company + currency + control-account bağlamında ayrı PartyAccount açılır.
- Kur ve para binary floating point kullanmaz; kullanılan kur sürümü değiştirilemez.
- Tenant/company scope ve yanlış şirket negatifleri DB/application katmanında korunur.

## Tasarım

- **Domain:** PartyAccount balance side `Receivable` veya `Payable` taşır. Açılış bakiyesi, hesaba yazılan ayrı ve immutable bir economic event'tir.
- **Veritabanı/migration:** `expand → migrate → contract`; mevcut sınıflandırılmamış hesaplar otomatik 120/320 tahminiyle dönüştürülmez. Yeni kayıtlar açık balance side ister; legacy satırlar veri sınıflandırma işiyle sonradan backfill edilir.
- **Muhasebe:** 120/320 ürün şablonu yalnız sürümlü varsayılan eşleme önerisidir. Kesin control account kimliği onaylı chart version'dan gelir.
- **Kur/rapor:** Günlük TRY bazlı rate snapshot; farklı raporlama dövizi TRY çaprazından, belge effective date'iyle yeniden üretilir. Eksik kur fail-closed ve eksik-gün listesi üretir.
- **Yetki:** Permission doğrudan kullanıcı/company scope'a veya kopyalanabilir şablon snapshot'ına bağlanır. Şablon değişikliği mevcut kullanıcıya sessiz yetki yükseltmez.
- **Audit/observability:** Account role/currency/control-account ve opening event değişimleri correlation, actor, effective/recorded tarih ve karar kaynağı taşır.
- **Deployment/rollback:** Migration eski okuyucuyu kırmaz. Contract aşaması veri sınıflandırma/mutabakat kanıtı olmadan çalışmaz; geri dönüş veri silmek yerine yeni özelliği kullanmayı durdurur.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Karar sicili ve modül politikaları | Karar, kanıt, yürürlük, review ve üretim sınırı kayıtlı | completed |
| 2 | PartyAccount balance side expand | Aynı Party + currency için AR ve AP hesapları; legacy uyumluluk | completed |
| 3 | Opening event kanıtı | Ayrı append-only event, scope/permission ve source uniqueness | completed |
| 4 | Authoritative Party report source adapter | Opening/due, original/counter impact ve restriction evidence aynı as-of/cutoff kesitinde explicit | completed |
| 5 | Gerçek PostgreSQL ve repository kapıları | 0033 boş/verili DB, RLS, lifecycle negatifleri ve full verify geçti | completed |
| 6 | Exact Party→GL control evidence ve job/sink | Active journal lineage checksum'a bağlı; gerçek due source persisted statement/aging/GL setine sıfır farkla yayımlandı | completed |
| 7 | Opening aging/open-item semantiği | `DEC-MP01-021`: explicit due/payment-term satırları, exact total ve allocation | validating |

## Test planı

- Unit/property: balance side zorunluluğu, aynı role/currency duplicate, farklı role izinli, para/date kuralları.
- DB integration: boş ve mevcut DB migration; legacy null sınıflandırma; AR+AP aynı Party/currency; cross-company RLS; append-only opening.
- Contract: source batch balance side/opening kanıtı; eksik/unclassified hesap fail-closed.
- Golden: opening + fatura + allocation/unallocation ile statement/aging/GL sıfır fark.
- Yetki: account/opening oluşturma ve maliyet raporu yanlış permission negatifleri.
- Kapsam dışı: Web/Android/API bu ilk persistence diliminde değişmez; public endpoint açılmaz.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-08-27 | Kullanıcı “kesinleşmiş kayıt değişebilir” dedi | Audit zinciri kaybolabilir | Yerinde edit reddedildi; yetkili reversal/correction, repository kuralları |
| 2026-08-27 | Kullanıcı kapalı dönem için tek yönetici istedi | Maker-checker ve hard-close kontrolü zayıflar | Reopen iki farklı onay; yönetici başlatır/onay grubundadır |
| 2026-08-27 | 120/320 resmi doğruluğu uzman onaysız | Yanlış mali eşleme riski | Sürümlü taslak; production ve yasal uyum beyanı bloklu |
| 2026-08-27 | Mevcut PartyAccount rol taşımıyor | Rapor balance side tahmin edilemiyor | Expand migration ve explicit classification |
| 2026-08-28 | Aktif posted-source okuması counter-event lifecycle'ını tek başına kanıtlamıyordu | Unallocation/reversal raporda original etkiyi yanlış canlandırabilirdi | Exact `NotPosted`/`Active`/`Reversed` lifecycle portu ve çift-ters negatif testi eklendi |
| 2026-08-29 | Control evidence portu source PartyAccount/journal setini almıyordu | Aynı tutarlı fakat ilgisiz GL bakiyesi reconciliation'ı yanlış yeşile çevirebilirdi | Exact posting lineage source checksum V2'ye ve control portuna bağlandı; eksik control line fail-closed |
| 2026-08-29 | Job reconciliation sonucunu oluşturup sıfır farkı zorunlu kılmıyordu | Farklı subledger/GL closing ile projection yayımlanabilirdi | Sıfır olmayan fark `PARTY_CONTROL_ACCOUNT_RECONCILIATION_DIFFERENCE` ile zero-publish |
| 2026-08-29 | Opening event due date/open-item kimliği taşımıyor | Opening statement ve GL'ye girse de aging/allocation semantiği yeniden üretilemez | Tahmin yapılmadı; explicit ürün kararı ve expand migration bekleyen milestone 7 açıldı |
| 2026-08-30 | Worker rapor refresh service identity ve company scope otoritesi tanımlı değildi | Zamanlanmış iş kullanıcıyı taklit edebilir veya yetki iptalinden sonra kapsam aşabilirdi | `DEC-MP01-020`: ayrı service identity, tek tenant, deployment + IAM company allow-list kesişimi ve `reporting.party-account.refresh` permission'ı; production provisioning açık |
| 2026-08-31 | Opening vadesi, banka kesinleşme anı ve refresh producer açık kaldı | Aging/allocation, reconciliation ve Worker işi tetikleme tamamlanamazdı | `DEC-MP01-021`–`023` önerileri kullanıcı tarafından onaylandı |
| 2026-08-31 | Her oturumda full regresyon tekrar ediliyordu | Geliştirme süresi ve çıktı maliyeti artıyordu | `DEC-MP01-024`: dilimde dar risk testi, MP validating/kapanışında birleşik full paket |
| 2026-08-31 | Yeniden derlenen unit/integration DLL'leri Windows Application Control tarafından `0x800711C7` ile engelleniyor | Dar opening runtime ve gerçek PostgreSQL kanıtı bu ortamda çalıştırılamıyor | Politika bypass edilmedi; iki ilgili proje 0 warning/error derlendi, runtime + DB golden MP validating paketinde açık kapı |

## İlerleme günlüğü

### 2026-08-27

- Kullanıcının 1–10 başlıklı kararları güvenlik ve finansal invariantlarla yorumlandı.
- KKTC Vergi Usul Yasası madde 114'te normal hesap döneminin takvim yılı olduğu ve özel dönem için Vergi Dairesi kararı gerektiği resmi kaynaktan doğrulandı.
- `DEC-MP01-001`–`009` ve `012` karar siciline; ORG/PARTY/IAM/GL/WF sözleşmeleri ilgili ürün politikalarıyla güncellendi. MP-03 karar-backed dilimler için `in-progress` oldu.
- `PartyAccountBalanceSide` domain sınıfı ile `0030_party_account_balance_side_expand` migration'ı eklendi. Due-schedule persistence explicit role kullanacak şekilde güncellendi.
- Standalone DB hostundaki eksik Parties.Contracts kaynak referansı tamamlandı; production davranışı değiştirmeyen test-host düzeltmesidir.
- `dotnet build KaguERP.slnx -c Release --no-restore`: 0 warning/error.
- `scripts/test-db.ps1`: boş DB'de 30/0 migration, gerçek PostgreSQL/RLS pass. Ayrı mevcut-verili provada 29 migration + legacy PartyAccount üzerine 0030/0 uygulandı; legacy satır korundu ve sınıflandırılmadı; tam RLS pass.
- Domain unit host: 60 check pass. Architecture host: 19 source project pass. `dotnet format --verify-no-changes`: pass.
- Test için workspace altında kurulan geçici PostgreSQL cluster durduruldu ve doğrulanmış hedef temizlendi; production veya kullanıcı DB/volume'una dokunulmadı.
- `Parties.Application` katmanı ve `party.opening-balance.create` permission gate'i eklendi. Opening source, fixed version `1`, debit/credit yönü, `numeric(20,4)` sınırı, effective/recorded zamanları ve authoritative PartyAccount rol/para/control-account snapshot'ıyla append-only saklanıyor.
- `0031_party_account_opening_event` migration'ı runtime rolüne yalnız SELECT/INSERT verir; RLS ve composite FK company izolasyonunu ve sahte account-context yazımını korur. Aynı source identity farklı payload/actor ile tekrar kullanılamaz; kaynak olay tek başına rapor bakiyesi üretmez.
- Mevcut yerel test DB'sinde 0030+0031 yükseltmesi (`2/0`) ve tam integration paketi geçti. Yalnız test için oluşturulan `kagu_erp_opening_blank_20260827_01` DB'sinde sıfırdan `31/0` migration ve aynı RLS/invariant paketi geçti; geçici DB doğrulanarak silindi ve test PostgreSQL kümesi durduruldu.
- Repository kapıları: Release build `0 warning/error`; domain host `62` check; architecture/API host `20` source project; `dotnet format --verify-no-changes` pass.
- Accounting tarafına exact source type/event/version/posting-purpose için effective-as-of + recorded-cutoff kullanan aktif posted-journal evidence loader eklendi. Posting öncesi kesit kaydı dışlar; linked exact reversal aynı kesitte etkinse original kanıtı dışlar, geçmiş kesiti değiştirmez. Gerçek PostgreSQL posting/reversal integration paketi geçti.
- Accounting exact evidence loader'ını Party opening/due adapter'ına bağlama işi 28 Ağustos diliminde tamamlandı; impact ve dispute/block eksikleri tahminle kapatılmadı.

### 2026-08-28

- `0032_party_due_source_posting_identity_expand`, due source effective date ve posting purpose alanlarını expand-compatible ekledi. 0031 seviyesindeki sabit legacy fixture migration sonrasında iki alanı `NULL` olarak korunarak kaldı; yeni eksik kimlikli insert DB check ile reddedildi ve loader eski satırı typed fail-closed durdurdu. Fixture test sonunda FK sırasıyla silindi ve tenant sayısı sıfır doğrulandı.
- `PostgresPartyReportSource`, PartyAccount/opening/due snapshot'larını repeatable-read ve transaction-local RLS kesitinde yükleyip Accounting'in exact active posted-source evidence portuyla modül referansı kurmadan birleştiriyor. Unposted/reversed kaynak, identity/date mismatch ve birden fazla aktif source version rapora etkisiz veya typed conflict'tir; watermark tüm aktif posting lineage'ının length-framed SHA-256 özetini taşır.
- Gerçek PostgreSQL testinde unposted opening/due sıfır etki verdi; exact journal posting'lerinden sonra `25 GBP` opening ve `75 GBP` due batch'e girdi; cross-company hesap görünmedi. Posting identity taşımayan allocation olayı kalan tutarı sessizce değiştirmek yerine `PARTY_REPORT_IMPACT_POSTING_IDENTITY_UNAVAILABLE` üretti.
- Boş `kagu_erp_party_source_blank_20260828_01` test DB'sinde `32/0` migration ve tam PostgreSQL/RLS paketi geçti; DB test sonunda silindi ve yokluğu doğrulandı. Mevcut verili test DB'sinde `0032` yükseltmesi `1/0` geçti. Release solution build `0 warning/error`, domain host `62` check, architecture/API host `20` source project ve `dotnet format --verify-no-changes` geçti. Standalone Integration DLL'i Windows Application Control `0x800711C7` ile engellediğinde güvenlik politikası değiştirilmedi; aynı linked test kaynağı izinli architecture `database` modunda çalıştırıldı.
- `0033_open_item_impact_source_identity_expand`, impact event'lerine canonical source type, pozitif version ve posting purpose ekledi. Pre-0033 legacy satır üç alanı `NULL` korunarak migration'dan geçti; yeni eksik kimlikli insert DB check ile, legacy load typed exception ile reddedildi. Writer retry'ı değişmiş source version'ı conflict sayıyor.
- Party source adapter original impact'leri exact Accounting evidence ile birleştiriyor. Unposted allocation remaining'i değiştirmedi; aynı `10 GBP` allocation exact journal posting'inden sonra `75 GBP` due'yu `65 GBP` yaptı.
- Mevcut verili DB'de `0033` yükseltmesi `1/0` geçti ve legacy impact korundu. Boş `kagu_erp_impact_source_blank_20260828_01` DB'sinde `33/0` migration ile tam PostgreSQL/RLS paketi geçti; geçici DB silindi ve yokluğu doğrulandı.
- Accounting source lifecycle okuması exact kaynağı `NotPosted`, `Active` veya original + reversal kanıtlı `Reversed` olarak üretir. Party adapter'ı original ve counter impact posting'lerini ayrı doğrular: unposted unallocation `65 GBP` kalanı değiştirmedi, posting sonrası remaining `75 GBP` oldu; original allocation journal'ı ayrıca reversed iken counter aktif bırakılan çift-ters senaryo fail-closed reddedildi.
- Lifecycle güncellemesinden sonra gerçek PostgreSQL/RLS paketi, Release solution build (`0 warning/error`), domain host (`62`), architecture/API host (`20`), format ve diff kontrolleri geçti.
- `0034_open_item_restriction_event` ile dispute ve collection block mutable flag yerine append-only applied/released stream oldu. `party.open-item-restriction.manage` permission gate'i, reason/effective/recorded/actor kanıtı, exact release, tek aktif kind ve stream chronology zorunlu. Due-line serialization için runtime UPDATE yetkisi genişletilmedi; fixed-search-path owner fonksiyonu `0035` ile security-definer çalışıyor.
- Authoritative restriction loader ve Party source adapter sıfır olayda `Clear`; aktif kombinasyonlarda `Disputed`, `Blocked` veya `DisputedAndBlocked` üretiyor. Late-recorded release geçmiş kesime sızmadı; farklı payload replay, ikinci aktif dispute, cross-company ID ve runtime UPDATE/DELETE reddedildi.
- Mevcut DB'de `0034+0035` sırasıyla `1/0` uygulandı; boş `kagu_erp_restriction_blank_20260828_01` DB'sinde `35/0` migration ve tam PostgreSQL/RLS paketi geçti, geçici DB silindi. İlk boş-DB tekrarı .NET 100 ns/PostgreSQL µs farkını yakaladı; restriction recorded-at domain girişinde µs hassasiyetine kanoniklenerek replay kararlı hale getirildi.
- Restriction değişikliğinde Release build `0 warning/error`, architecture/API host `20`, format ve diff kapıları geçti. Domain host `63` testi ilk çalışmada geçti; sonraki yeniden üretilmiş Parties DLL'i Windows Application Control tarafından `0x800711C7` ile engellendi, güvenlik politikası bypass edilmedi.
- Sıradaki kesin adım: opening event'in explicit due date/payment-term ve allocation yapılabilir open-item davranışını ürün kararıyla kapatıp opening dahil source→statement/aging→GL golden cross-foot'u çalıştırmak.

### 2026-08-29

- Party source checksum V2, aktif posted journal ID ve exact source type/event/version/purpose ile effective/recorded/posted kesimini canonical `PostingLineage` seti olarak kapsıyor; changed journal checksum'ı değiştiriyor ve koleksiyon defensive copy ile korunuyor.
- Accounting-owned exact control-account loader her Party journal'ının header bağlamını ve seçili control-account satırını yeniden doğruluyor; etkin reversal, eksik satır veya changed lineage fail-closed. Reporting adapter aynı source fact'lerinden subledger'ı bağımsız türetip GL evidence ile aynı report slice'ta birleştiriyor.
- Gerçek PostgreSQL'de fonksiyonel parası TRY olan posted `75 GBP` due source, projection job ve atomic sink üzerinden persisted statement=`75 GBP`, aging=`75 GBP`, subledger=`75 GBP`, GL transaction görünümü=`75 GBP` olarak yayımlandı; aynı command replay'i yeni fact üretmedi. Wrong-company, wrong-transaction-currency ve eksik control-line negatifleri geçti.
- Aynı gerçek fixture'ın `25 GBP` opening + `75 GBP` due + `10 GBP` allocation + `10 GBP` unallocation kesiti subledger ve GL tarafında `110 debit - 10 credit = 100 GBP` sıfır fark verdi.
- Full opening→aging golden henüz kapatılmadı: opening event ayrı due date/payment-term/open-item kimliği taşımıyor. `effective date = due date` veya aging dışlama varsayımı yapılmadı; opening'i allocation yapılabilir aging kalemine dönüştürecek ürün kararı bekleniyor.
- Release solution build `0 warning/error`; domain host `63`, architecture/API host `20`; migration idempotency `0/0` ve tam PostgreSQL tenant/company RLS integration paketi geçti.
- Şirket aging policy'si append-only definition stream'e taşındı. Effective date ve recorded cutoff kesiminde en yüksek uygun sürümü seçen PostgreSQL adapter gerçek golden job'a bağlandı; sabit test policy kaynağı kaldırıldı. Policy kimliği/sürüm sırası, tam bucket kapsamı, cross-company RLS ve append-only runtime privilege negatifleri geçti.
- `0036+0037` mevcut DB'ye ileri uygulanıp son yükseltmede `1/0` idempotent kaldı; ayrı boş DB'de `37/0` migration ve tam RLS integration paketi geçti. Runtime append-only policy insert ayrıca kanıtlandı. Geçici DB silindi, test PostgreSQL kümesi durduruldu.
- Production Party report sözleşmesi `party.account.detail` version `1` ve `reporting.party-account.view` olarak uygulandı. `POST /api/v1/reports/{code}/queries`, company/permission kontrolünü projection lookup'tan önce yapıyor; allowed, denied ve not-found audit kararlarını aynı PostgreSQL transaction'ında kalıcılaştırıyor. API yalnız Application portuna bağlı, PostgreSQL adapter ve ortak audit appender Bootstrap composition root'ta bağlanıyor.
- Production query eklerinden sonra Release solution build `0 warning/error`, domain host `63`, architecture/API host `20`, format ve gerçek PostgreSQL tenant/company RLS paketi geçti; migration tekrarı `0/0` kaldı. Opening-aging ürün kararı değiştirilmedi.

### 2026-08-31

- `DEC-MP01-021` uygulandı: yeni opening draft bir veya daha fazla explicit due date/payment-term snapshot satırı ve due-schedule kimliği olmadan üretilemiyor; satır toplamı source amount'a exact eşit ve kimlikler tekil.
- Opening writer authoritative PartyAccount'tan Party/currency/control-account/balance-side bağlamını alıyor, receivable için debit ve payable için credit doğal yönünü zorunlu kılıyor ve opening event ile allocatable due schedule'ı aynı caller transaction'ında idempotent saklıyor.
- Mevcut `party.due_schedule` altyapısı yeterli olduğundan yeni tablo veya migration üretilmedi. Legacy opening kayıtlarına vade uydurulmadı; yalnız yeni settleable opening'ler due/open-item yoluna giriyor.
- Party report source, due schedule'a bağlanmış opening'i eski `OpeningExposure` toplamından çıkarıp normal open-item olarak tek kez okuyor. Böylece aging, oldest-due allocation ve restriction altyapısı opening için de aynen kullanılıyor; legacy opening davranışı korunuyor.
- Unit harness test-adı filtresi aldı; filtresiz koşu MP kapanışındaki tam unit paketidir. Etkilenen Unit ve Integration projeleri ayrı ayrı `0 warning / 0 error` derlendi, `git diff --check` temiz geçti. Dar runtime test Windows Application Control `0x800711C7` nedeniyle çalışmadı; gerçek PostgreSQL opening→aging→GL golden ile birlikte MP validating kapısında açık kalır.

## Tamamlanma kanıtı

- [x] Karar sicili ve modül sözleşmeleri.
- [x] Migration ve compatibility kanıtı.
- [x] Tenant/company negatif testleri.
- [x] Opening event append-only ve idempotency kanıtı.
- [ ] Source→statement/aging→GL cross-foot (opening open-item implementasyonu derlendi; runtime/gerçek PostgreSQL tekrarları Application Control engeli nedeniyle validating).
- [x] Çalıştırılan komutlar ve sonuçlar.
- [x] MP-03 kapı etkisi MASTER_PLAN içinde güncel.
