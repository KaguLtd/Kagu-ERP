# Teslim Yol Haritası

## 1. Yaklaşım

Takvim tahmindir; ekip, mevcut veri kalitesi, resmi KKTC onayları ve entegrasyon erişimleri görülmeden sözleşme değildir. Program sırası ve güncel kapı durumları için root [MASTER_PLAN.md](../../MASTER_PLAN.md) otoritedir; bu belgedeki takvim fazları kapasite tahmini olarak kullanılır. Fazlar kısmen örtüşebilir, fakat kalite/onay kapısı geçmeden sonraki kritik bağımlılık canlı sayılmaz.

Önerilen çekirdek ekip:

- 1 ürün sahibi/iş analisti,
- 1 teknik lider/backend,
- 1–2 backend/full-stack,
- 1 web frontend,
- 1 Android (mobil fazında),
- 1 QA automation,
- part-time DevOps/security,
- yetkili KKTC mali müşavir ve mevzuat danışmanı,
- pilot rollerde depo/satış/finans temsilcileri.

Tek geliştirici + Codex ile de yapılabilir; süre ve bağımsız kontrol ihtiyacı belirgin artar. Codex hızlandırıcıdır, mali/hukuki onay mercii değildir.

## 2. Faz 0 — Keşif ve kararlar (2–4 hafta)

Çıktılar:

- firma/şube/depo/kullanıcı/hacim envanteri,
- Logo/mevcut veri ve rapor envanteri,
- iş süreç haritası ve ilk pilot kapsamı,
- KKTC hukuk/resmi onay sorularının kurumlara açılması,
- server/ağ/domain/backup ve veri konumu kararı,
- golden mali senaryolar,
- MVP backlog ve risk register.

Kapı: ürün sahibi, teknik lider, mali müşavir; belirsiz e-fatura/beyan/yazar kasa alanı varsayım olarak işaretli.

## 3. Faz 1 — Platform omurgası (4–6 hafta)

- Monorepo, build/test/CI, container ve staging.
- PostgreSQL migration, modül şemaları, outbox/audit.
- Keycloak OIDC/MFA, rol/permission/company scope.
- Caddy/TLS, config/secrets, OTel.
- pgBackRest/restic, ilk tam restore tatbikatı.
- Web kabuk ve tasarım sistemi; OpenAPI client.

Kapı: RLS/authz negatif testi, backup/restore, deployment ve temel SLO.

## 4. Faz 2 — Ticari ve stok çekirdeği (6–8 hafta)

- Organizasyon/ana veriler.
- Cari hesap/master ve açık kalem iskeleti.
- Stok kartı, depo, birim, lot/seri, hareket ve rezervasyon.
- Satış teklif/sipariş/sevk/fatura/iade.
- Satın alma talep/sipariş/kabul/fatura/üçlü eşleme.
- Belge/ek, onay ve operasyon raporları.

Kapı: siparişten faturaya ve satın almadan kabule E2E; stok/cari mutabakat; pilot UX testi.

## 5. Faz 3 — Finans ve muhasebe (6–8 hafta)

- Banka/kasa, ödeme/tahsilat, ekstre/mutabakat.
- Çek/senet portföyü ve risk.
- Muhasebe event/posting, hesap planı, fiş, mizan.
- Kur/değerleme, dönem kapanış ve alt defter mutabakatları.
- Mali raporlar ve maker-checker.

Kapı: golden company tüm mali akışları, borç=alacak, sıfır beklenmeyen mutabakat farkı; mali müşavir kabulü.

## 6. Faz 4 — KKTC uyum ve e-fatura (6–10 hafta; resmi süreç paralel)

- Resmi Tekdüzen plan/eşleme ve yazılım onay süreci.
- Tarih etkili KDV/vergi motoru ve çalışma dosyası.
- UBL-KKTC, şema doğrulama, numaralama ve arşiv.
- Portal iş akışı veya onaylı doğrudan entegrasyon.
- E-fatura iptal/olay/restore ve resmi sandbox testleri.
- KVKK/veri transferi ve saklama uygulaması.

Kapı: resmi test/izin veya belgelenmiş portal modu; mali müşavir/hukuk kabulü. Bu kapı takvimin kritik dış bağımlılığıdır.

## 7. Faz 5 — Android dar kapsam (4–6 hafta)

- PKCE/MFA ve cihaz güvenliği.
- Onay görevleri, cari/stok görüntüleme.
- Depo barkod/sayım ve offline-read-first cache.
- Push yenileme, sync/idempotency/conflict.
- Yönetilen pilot dağıtım.

Kapı: cihaz envanteri, kayıp cihaz/oturum iptali, offline/sync testleri ve saha kullanıcı kabulü.

## 8. Faz 6 — Veri taşıma ve pilot (4–6 hafta)

- En az iki tam migration provası.
- Kaynak–hedef mali/stok/cari mutabakatı.
- Rol bazlı eğitim ve runbook.
- Sınırlı şube/iş grubu pilotu.
- Dönem sonu simülasyonu, DR ve incident tabletop.
- Go-live ve yoğun destek.

Kapı: [release kabul planı](../quality/05-release-and-acceptance.md).

## 9. İlk 90 gün somut planı

### Gün 1–30

- Faz 0 tamamla; resmi soruları gönder.
- Repo/CI/local Compose iskeleti.
- Keycloak + company scope + PostgreSQL RLS teknik spike.
- UI token/kabuk prototipi.
- Server/backup proof-of-concept ve restore.

### Gün 31–60

- Organizasyon, permission ve audit dikey dilimi.
- Cari master/list/detail API + web.
- Stok ana veri/depo/hareket çekirdeği.
- Golden test şirketi ve OpenAPI pipeline.
- Migration kaynak profilinin ilk raporu.

### Gün 61–90

- Satış siparişi→rezervasyon→sevk ilk dikey akışı.
- Posting motoru iskeleti ve dengeli fiş property testleri.
- Belge/outbox/notification altyapısı.
- Staging Linux deploy + backup/restore tatbikatı.
- İlk kullanıcı görev testi ve backlog düzeltmesi.

## 10. Bağımlılıklar ve kritik yol

- Resmi e-fatura/onay ve teknik sandbox erişimi.
- Yetkili mali müşavirce hesap/vergi/rapor kabulü.
- Kaynak Logo/veri export erişimi ve veri kalitesi.
- Banka dosya/API örnekleri ve sözleşmeleri.
- Sunucu, DNS, uzak yedek ve veri lokasyonu.
- Pilot kullanıcı zamanı ve karar yetkisi.

Kod ekibi bu bağımlılıkları beklerken adapter fake/portal modu, golden data ve diğer çekirdek modülleri ilerletir; resmi sonucu kod içinde tahmin etmez.

## 11. MVP ve ertelenenler

MVP: tek grup içindeki birden fazla şirket/şube, rol/scope, cari, stok, satış, satın alma, banka/kasa, çek/senet, GL, KDV çalışma alanı, e-fatura güvenli yolu, temel rapor, web ve dar Android, backup/restore/audit.

Sonraki faz: bordro/İK, üretim MRP, gelişmiş CRM, e-ticaret, BI warehouse, iOS, çok ülkeli vergi, Kubernetes/mikroservis, gelişmiş tahmin/AI. Yeni kapsam ADR + ürün önceliği + operasyon maliyeti ister.

## 12. Başarı ölçüleri

- Pilot işlerin en az %95'i sistem içinde tamamlanıyor.
- Mali mutabakatlarda açıklanamayan fark sıfır.
- Çift ödeme/e-fatura ve şirketler arası sızıntı sıfır.
- Kritik görev tamamlama süresi kullanıcı kabul hedefinde.
- SLO, crash ve incident hedefleri sağlanıyor.
- Restore tatbikatları RPO/RTO içinde.
- Dönem kapanışı kanıtlı ve mali müşavirce kabul edilmiş.

## 13. v1.1 yol haritası düzeltmeleri

Mevcut faz sırası korunur; aşağıdaki gate’ler ilgili milestone’a eklenir:

### Accounting foundation gate

- REA/process/control artefaktları ve ortak sözlük;
- immutable source/subledger/GL model;
- due schedule, allocation ve bank settlement ayrımı;
- effective/recorded/posted time ve separate lock scopes;
- posting exception/repost;
- golden cycle + control-account reports.

Bu gate geçmeden satış/satın alma/stok modülleri kendi GL shortcut’ını yazamaz.

### Pilot gate

- role/process owner ve eğitim materyali;
- master data cleansing ve mapping owner’ları;
- bank/e-Fatura örnek/profile doğrulaması;
- iki paralel ay kapanış;
- manual continuity ve exception runbook;
- 30/60/90 gün benefit/adoption baseline.

### Scale gate

Read replica, broker, ayrı reporting store veya servis ayrıştırma; yalnız ölçülen close/report/posting SLO, backlog, security veya failure-domain ihtiyacıyla ADR’ye girer.

## 14. İlk uygulama sırası

1. Company/period/account/party ve permission scope.
2. Accounting kernel + source-linked manual test event.
3. Party due schedule/open item.
4. Treasury payment.
5. Allocation/unallocation.
6. Bank statement/reconciliation.
7. GL/control-account/aging/statement reports.
8. Reversal, posting exception ve projection repost.
9. Web workbench; ardından read-first Android.
10. Sales/purchasing/inventory cycles.

Bu sıra ilk dikey dilimde “tahsilat oldu” ile “fatura kapandı” ve “banka doğruladı” kavramlarını erken ayırır.

## 15. Program başarı ölçümü

Go-live dışında aylık:

- close cycle time ve unresolved reconciliation;
- posting exception/reversal/duplicate;
- manual spreadsheet/workaround;
- eğitim tamamlama ve görev başarı oranı;
- data quality ve control evidence completion;
- support ticket ve user confidence;
- planlanan iş faydası

ölçülür. Sahipsiz veya kanıtsız KPI başarı ilanı için kullanılmaz.
