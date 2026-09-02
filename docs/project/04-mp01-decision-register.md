# MP-01 Firma Politikaları Karar Kaydı

Bu kayıt, `MASTER_PLAN.md` içindeki MP-01 minimum karar setini yürütür. Bir satırın bulunması kararın verilmiş olduğu anlamına gelmez. `approved` durumuna geçmek için yetkili kişi, tarih, kanıt ve yürürlük bilgisi zorunludur.

## 1. Sorumlu roller

| Rol | Sorumluluk | İsim | Durum |
|---|---|---|---|
| Ürün sahibi | Kapsam, öncelik, şirket politikaları ve kullanıcı kabulü | atanmadı | open |
| Teknik lider | Mimari, repository, veri/API ve teknik risk | atanmadı | open |
| Yetkili mali müşavir/muhasip | Hesap planı, posting, dönem, vergi ve mali rapor | atanmadı | open |
| Güvenlik/veri sorumlusu | Yetki, kişisel veri, saklama ve dış transfer | atanmadı | open |
| Operasyon sorumlusu | Linux, RPO/RTO, backup/restore, bakım ve olay | atanmadı | open |

İsimli atama yapılmadan ilgili karar `approved` olamaz. Aynı kişi birden çok rolü üstlenebilir; kritik kararların bağımsız onay ve görevler ayrılığı ihtiyacı ayrıca korunur.

## 2. Karar kayıt standardı

Her karar şu alanları taşır:

```yaml
decision_id: DEC-MP01-000
title: "..."
status: open | requested | evidence-received | approved | superseded
authority: "Yetkili kişi/kurum"
owner_role: "..."
owner_name: "..."
decision: "..."
source_or_evidence: "Erişim kontrollü dosya/URL/tutanak referansı"
effective_from: YYYY-MM-DD
review_due: YYYY-MM-DD
blocks: [MP-02 | MP-03 | feature:<code> | production]
affected_requirements: [ORG, GL]
notes: "..."
```

Secret, credential, gerçek kişisel veri, tam VKN/IBAN veya hassas kurum cevabı bu Markdown dosyasına gömülmez; erişim kontrollü kanıta referans verilir.

## 3. Başlangıç karar matrisi

| ID | Karar | Sahip rol | Durum | Açıkken blokladığı alan | Güvenli paralel iş |
|---|---|---|---|---|---|
| DEC-MP01-001 | Tenant ve tüzel şirket sayısı; verinin ortak/ayrı sınırı | Ürün + teknik | approved | Production isimli sahip/kabulü | Tek tenant altında yönetilebilir çok company ve RLS |
| DEC-MP01-002 | Şube, depo, kasa, banka hesabı, proje ve masraf merkezi kapsamı | Ürün + muhasebe | approved | İlgili production kabulü | Şube-ready, çok depo ve proje boyutu; cost center ayrı/opsiyonel |
| DEC-MP01-003 | Mali yıl/dönem takvimi ve GL/tax/inventory/hard lock politikası | Muhasebe | approved | Resmi düzeltme/reopen üretim usulü | Takvim yılı ve aylık dönem; fail-closed lock modeli |
| DEC-MP01-004 | Fonksiyonel, işlem ve raporlama para birimleri | Muhasebe + ürün | approved | Production mali müşavir kabulü | TRY functional; TRY/USD/EUR/GBP account currency |
| DEC-MP01-005 | Kur kaynağı, rate type, onay ve override politikası | Muhasebe | approved | Production mali müşavir kabulü | Günlük manuel TRY bazlı şirket efektif kuru |
| DEC-MP01-006 | Yuvarlama, scale ve residual politikası | Muhasebe | approved | Production mali müşavir kabulü | 2 hane görünüm, 4 hane para, 3 hane kur girişi; explicit residual |
| DEC-MP01-007 | KKTC hesap planı başlangıç sürümü ve şirket alt hesap politikası | Muhasebe | approved | Resmi chart/uzman kabulü ve production | 120/320 sürümlü geliştirme şablonu; alt hesap açılabilir |
| DEC-MP01-008 | Posting rule, manual journal, reversal/correction ve repost onay politikası | Muhasebe + güvenlik | approved | Production mali müşavir kabulü | Kaynak belge, direct journal ve reversal ayrımı |
| DEC-MP01-009 | Cari vade, taksit, allocation/unallocation, avans/fazla ödeme ve write-off | Muhasebe + ürün | approved | Production mali müşavir kabulü | Peşin default, oldest-due, unapplied credit ve aging policy |
| DEC-MP01-010 | Banka/tahsilat/payment ile reconciliation tetikleyicileri ve transit hesaplar | Muhasebe + finans | approved | Production hesap eşlemesi/banka kabulü | `DEC-MP01-022` ayrıntısındaki posted payment + transit reconciliation |
| DEC-MP01-011 | Stok değerleme, eksi stok, backdate/repost ve sayım politikası | Muhasebe + ürün | open | MP-04 stok/satış | Generic quantity invariants ve impact-preview contract |
| DEC-MP01-012 | Rol kataloğu, permission/scope, SoD, quorum, limit ve delegation | Ürün + güvenlik | approved | Production access review ve isimli güvenlik sahibi | Granüler permission + altı kopyalanabilir şablon; SoD korunur |
| DEC-MP01-013 | KDV/tax point/beyan/düzeltme kural sahibi ve resmi yayın süreci | Muhasebe + hukuk | open | TAX/EINV feature ve production | Tarih etkili rule engine/adapter iskeleti; gerçek oran yok |
| DEC-MP01-014 | e-Fatura portal/doğrudan entegrasyon, numara, imza, retry, iptal ve arşiv | Ürün + muhasebe + operasyon | open | EINV production | Fake/portal adapter contract; gerçek gönderim kapalı |
| DEC-MP01-015 | Kişisel veri sınıfları, saklama, legal hold ve dış transfer | Güvenlik/veri | open | production ve dış servis seçimi | Local/sentetik veriyle geliştirme; dış aktarım kapalı |
| DEC-MP01-016 | Banka formatları, ödeme entegrasyonu, credential ve mutabakat yetkileri | Finans + güvenlik | open | Banka entegrasyonu/production | Fake provider ve örnek sentetik dosya parser contract |
| DEC-MP01-017 | RPO, RTO, backup lokasyonu, bakım penceresi ve on-call | Operasyon + ürün | open | MP-02 çıkış kapısının restore hedefi ve production | Local restore smoke; hedefler şartnamedeki öneri olarak etiketli |
| DEC-MP01-018 | Git repository sınırı: bağımsız `Kagu ERP` repository'si veya üst monorepo | Teknik + ürün | approved | — | Bağımsız repository, `main` dalı ve GitHub `origin` doğrulandı |
| DEC-MP01-019 | İsimli proje sahiplerinin atanma zamanı | Kullanıcı/repository sahibi | approved | Production ve uzman kabulü | Roller `atanmadı` kalır; karar kanıtlı geliştirme ilerler |
| DEC-MP01-020 | Background Worker servis kimliği ve şirket kapsamı | Ürün + güvenlik + teknik | approved | Production secret/identity provisioning | Ayrı servis kimliği, tek tenant ve iki taraflı company allow-list |
| DEC-MP01-021 | Açılış bakiyesinin vade, aging ve allocation davranışı | Ürün + muhasebe | approved | — | Zorunlu vadeli bir/çok open-item satırı; oldest-due allocation |
| DEC-MP01-022 | Ödeme/tahsilat ile banka mutabakatının muhasebe anı | Muhasebe + finans | approved | Gerçek banka formatı/credential | Posted ekonomik olay + transit hesap; reconciliation yalnız doğrular |
| DEC-MP01-023 | Party rapor projection yenileme tetikleyicisi | Ürün + teknik | approved | Recurring schedule UI/API | İlk sürümde yetkili manuel, idempotent enqueue |
| DEC-MP01-024 | Geliştirme ve MP kapanış test kadansı | Kullanıcı + teknik | approved | — | Dilimde dar risk testi; tam regresyon MP kapanışında |

### DEC-MP01-001 — Tenant ve şirket topolojisi

```yaml
decision_id: DEC-MP01-001
title: "Tenant ve yasal şirket topolojisi"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Ürün sahibi + teknik lider"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Tek tenant Kagu Ltd.'dir. Tenant altında yönetici tarafından birden çok yasal Company açılabilir, etkinleştirilebilir veya ileriye dönük kapatılabilir; company verisi ayrı scope ve defterdir."
source_or_evidence: "2026-08-27 kullanıcı kararı; docs/project/plans/2026-08-27-mp01-business-policy-baseline.md"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-owner-acceptance]
affected_requirements: [ORG, DATA, IAM]
notes: "Kapatma hard delete değildir. Tenant/company kimlikleri yeniden kullanılmaz; cross-company RLS ve application scope zorunludur."
```

### DEC-MP01-002 — Organizasyon kapsamı

```yaml
decision_id: DEC-MP01-002
title: "Şube, depo, proje ve maliyet merkezi kapsamı"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Ürün sahibi + muhasebe sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Şube ilk yayında opsiyoneldir fakat altyapısı bulunur. Birden çok depo açılabilir, pasifleştirilebilir ve depolar arası sevk yapılabilir. Project belge/fiş boyutudur. CostCenter proje dışı bölüm veya gider sorumluluğu boyutudur; altyapıda bulunur ve şirketçe opsiyonel etkinleştirilir."
source_or_evidence: "2026-08-27 kullanıcı kararı"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-scope-acceptance]
affected_requirements: [ORG, INV, GL, RPT, IAM]
notes: "Depo, şube, proje ve cost center aynı kimlik değildir. Kullanılmış master silinmez; tarih etkili pasifleştirilir."
```

### DEC-MP01-003 — Mali dönem ve kilitler

```yaml
decision_id: DEC-MP01-003
title: "Mali yıl, aylık dönem ve yeniden açma"
status: approved
authority: "KaguLtd repository sahibi + KKTC Merkezi Mevzuat Dairesi"
owner_role: "Muhasebe sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Varsayılan mali yıl 1 Ocak–31 Aralık'tır. Aylık FiscalPeriod kayıtları her zaman modellenir; aylık operasyonel kapanış şirket ayarıyla etkinleştirilebilir. Soft-close özel yetki ve gerekçe ister. Hard-close yeniden açma iki farklı onay, gerekçe, süre, kapsam ve audit ister."
source_or_evidence: "2026-08-27 kullanıcı kararı; KKTC 27/1977 Vergi Usul Yasası md.114, https://mevzuat.gov.ct.tr/Portals/48/27-1977%20VERGI%20USUL%20YASASI.pdf"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-correction-procedure]
affected_requirements: [ORG, GL, TAX, INV, WF]
notes: "Yasa normal hesap dönemini takvim yılı olarak tanımlar; özel on iki aylık dönem Vergi Dairesi kararı ister. Yönetici tek başına hard-close açamaz."
```

### DEC-MP01-004 — Para birimleri

```yaml
decision_id: DEC-MP01-004
title: "Fonksiyonel, hesap ve raporlama para birimleri"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Ürün sahibi + muhasebe sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Başlangıç Company functional currency TRY'dir. PartyAccount tek bir TRY, USD, EUR veya GBP işlem para birimi taşır. Aynı Party rol ve para başına ayrı hesaba sahip olabilir. Rapor hedef para seçebilir ve her kaynak olayı kendi effective-date kuruyla dönüştürür."
source_or_evidence: "2026-08-27 kullanıcı kararı"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-accounting-acceptance]
affected_requirements: [ORG, PARTY, GL, RPT]
notes: "Fonksiyonel para cari bazında seçilmez. İlk posted hareketten sonra company functional currency yerinde değişmez. Cross-currency PartyAccount allocation ayrıca onaylı FX politikası ister."
```

### DEC-MP01-005 — Günlük manuel kur

```yaml
decision_id: DEC-MP01-005
title: "TRY bazlı günlük manuel şirket efektif kuru"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Muhasebe sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "USD/TRY, EUR/TRY ve GBP/TRY şirket efektif kurları yetkili kullanıcı tarafından günlük manuel girilir. Ek insan onayı yoktur; permission ve audit zorunludur. Belge ve rapor kullanılan immutable rate version snapshot'ına bağlanır."
source_or_evidence: "2026-08-27 kullanıcı kararı"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-accounting-acceptance]
affected_requirements: [ORG, GL, PARTY, RPT, IAM]
notes: "Kullanılmış oran yerinde değiştirilmez; düzeltme yeni version'dır. Kur eksikse posting/report fail-closed olur. Alış/satış banka kuru değil, açık rate_type=company_effective kullanılır."
```

### DEC-MP01-006 — Hassasiyet ve yuvarlama

```yaml
decision_id: DEC-MP01-006
title: "Para, kur ve yuvarlama politikası"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Muhasebe sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Kullanıcı görünümü tr-TR biçiminde iki ondalıktır. Para numeric(20,4), oran numeric(28,12) saklanır; manuel kur girişi varsayılan üç ondalık gösterir. Ticari tutar son iki haneye MidpointRounding.AwayFromZero ile yuvarlanır."
source_or_evidence: "2026-08-27 kullanıcı kararı; mevcut veri ve currency evidence sözleşmeleri"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-accounting-acceptance]
affected_requirements: [DATA, GL, PARTY, RPT]
notes: "Görünüm hassasiyeti DB hassasiyeti değildir. Dengeli fişte residual oluşursa sessizce atılmaz; sürümlü rounding account/purpose satırında gösterilir."
```

### DEC-MP01-007 — Hesap planı politikası

```yaml
decision_id: DEC-MP01-007
title: "120/320 cari kontrol hesabı geliştirme şablonu"
status: approved
authority: "KaguLtd repository sahibi — geliştirme politikası"
owner_role: "Muhasebe sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Versioned başlangıç şablonu müşteri/alacak hesaplarını 120, tedarikçi/borç hesaplarını 320 ailesi altında önerir. Şirketler ortak şablondan türeyebilir ve postalanabilir alt hesap açabilir. Barter gibi 12x/32x varyantları kod içine gömülmez; yönetici taslağı ve mali müşavir kabulüyle chart version'da yayımlanır."
source_or_evidence: "2026-08-27 kullanıcı kararı; KKTC Vergi Dairesi Tekdüzen Hesap Planı yayın noktası"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production, official-chart-import, accountant-acceptance]
affected_requirements: [GL, PARTY, MIG]
notes: "Bu karar 120/320'nin tüm şirket türleri için kendiliğinden resmi uygunluk beyanı değildir. Kullanılmış hesap silinmez ve kodu yeniden kullanılmaz."
```

### DEC-MP01-008 — Posting ve düzeltme

```yaml
decision_id: DEC-MP01-008
title: "Kaynak belge, direct manual journal ve kesin kayıt düzeltmesi"
status: approved
authority: "KaguLtd repository sahibi + repository finansal/güvenlik kuralları"
owner_role: "Muhasebe sahibi + güvenlik sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Yetkili kullanıcının elle girdiği satış/alış belgesi ayrıca insan onayı olmadan post edilebilir; otomatik journal da varsayılan olarak ek onay istemez. Kaynaksız/direct manual GL journal kritik akıştır ve hazırlayandan farklı tek yönetici onayı ister. Kesinleşmiş belge/fiş yerinde değişmez; yetkili reversal/correction üretir."
source_or_evidence: "2026-08-27 kullanıcı kararı; AGENTS.md değişmez finansal ve SoD kuralları"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-accounting-acceptance]
affected_requirements: [GL, WF, IAM, DOC]
notes: "Repost yalnız aynı doğru kaynak ve rule snapshot'ından türetilmiş projection/ledger yeniden kurar; yeni ticari gerçek oluşturmaz."
```

### DEC-MP01-009 — Cari vade, allocation ve aging

```yaml
decision_id: DEC-MP01-009
title: "Cari vade, allocation, fazla ödeme, write-off ve aging"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Ürün sahibi + muhasebe sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Varsayılan payment term peşindir; cari açılışında peşin/30/60/90/120 gün seçilebilir. Otomatik öneri oldest due first'tür. Fazla ödeme unapplied credit/avans olarak kalır ve sonraki en eski faturaya allocation önerilir. Write-off ayrı permission ve hazırlayandan farklı tek yönetici onayı ister."
source_or_evidence: "2026-08-27 kullanıcı kararı"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-accounting-acceptance]
affected_requirements: [PARTY, TRY, GL, RPT, WF]
notes: "Aging bucket'ları future, due-now, 1-30, 31-60, 61-90, 91-120 ve 121+ gündür. Disputed/blocked toplam bakiyeye dahildir fakat ayrı subtotal/flag taşır."
```

### DEC-MP01-010 — Banka mutabakat tetikleyicileri

```yaml
decision_id: DEC-MP01-010
title: "Payment posting, transit hesap ve reconciliation tetikleyicileri"
status: approved
authority: "KaguLtd repository sahibi — DEC-MP01-022 ayrıntısı kabul edildi"
owner_role: "Muhasebe sahibi + finans sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Payment/receipt yetkili kesinleştirmede ekonomik olay ve GL sonucu üretir. Banka kesinleşmesi bekleniyorsa transit/outstanding hesap kullanılır; statement reconciliation yeni payment üretmez, transit hesabı kapatan ayrı kanıttır. İlk tolerans sıfırdır ve farklar ayrı source event'tir."
source_or_evidence: "2026-08-31 kullanıcı kararı; DEC-MP01-022"
effective_from: 2026-08-31
review_due: 2027-06-30
blocks: [production-account-mapping, production-bank-provider-acceptance]
affected_requirements: [TRY, GL, PARTY, WF]
notes: "Provider formatı, credential ve kesin chart account kodu bu ürün kararından türetilmez."
```

### DEC-MP01-012 — Granüler yetki ve başlangıç şablonları

```yaml
decision_id: DEC-MP01-012
title: "Granüler permission ve sade kopyalanabilir yetki setleri"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Ürün sahibi + güvenlik sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Yetki doğrudan resource.action + company/alt scope ve koşul üzerinden değerlendirilir. Altı başlangıç şablonu Sistem/Şirket Yöneticisi, Muhasebe, Satış, Satınalma, Depo ve Finans'tır. Kullanıcı yetkileri kopyalanabilir snapshot'tır. Tutar limitleri altyapıda bulunur fakat varsayılan kapalıdır. Maliyet/marj ve raporlar ayrı permission'dır."
source_or_evidence: "2026-08-27 kullanıcı kararı; IAM ve workflow sözleşmeleri"
effective_from: 2026-08-27
review_due: 2027-06-30
blocks: [production-access-review]
affected_requirements: [IAM, WF, RPT, SEC]
notes: "Kritik işlem hazırlayan kendi işlemini onaylayamaz. Genel kritik akışta farklı tek yönetici yeterlidir; hard-close reopen repository kuralı gereği iki farklı onaydır. Şablon değişimi mevcut kullanıcıyı sessizce yükseltmez."
```

### DEC-MP01-018 — Bağımsız Git repository sınırı

```yaml
decision_id: DEC-MP01-018
title: "Kagu ERP Git repository sınırı"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Ürün sahibi + teknik lider"
owner_name: "İsim teyidi bekliyor; karar yetkili kullanıcı tarafından verildi"
decision: "Kagu ERP klasörü bağımsız Git repository olacak; varsayılan dal main ve origin https://github.com/KaguLtd/Kagu-ERP.git olacak."
source_or_evidence: "2026-08-19 kullanıcı talimatı; git ls-remote ile erişilebilir ve boş remote doğrulaması"
effective_from: 2026-08-19
review_due: 2027-08-19
blocks: []
affected_requirements: [ARCH, OPS, REL]
notes: "Yerel repository 2026-08-19 tarihinde main dalıyla başlatıldı; commit veya push bu karar kaydı sırasında yapılmadı."
```

### DEC-MP01-019 — İsimli sahip atamasını erteleme

```yaml
decision_id: DEC-MP01-019
title: "İsimli proje sahiplerinin atanma zamanı"
status: approved
authority: "KaguLtd repository sahibi — bu görevdeki kullanıcı talimatı"
owner_role: "Kullanıcı/repository sahibi"
owner_name: "İsim verilmedi"
decision: "Ürün, teknik, muhasebe, güvenlik/veri ve operasyon rolleri şimdilik atanmadı kalacak; geliştirme sonuna doğru yeniden değerlendirilecek."
source_or_evidence: "2026-08-21 kullanıcı talimatı"
effective_from: 2026-08-21
review_due: 2027-06-30
blocks: [production, MP-03-business-acceptance]
affected_requirements: [GL, IAM, SEC, OPS, DR, REL]
notes: "Karar yalnız geri döndürülebilir ve politika bağımsız teknik geliştirmeyi serbest bırakır. Gerçek muhasebe, vergi, yetki, RPO/RTO veya production kabulü değildir."
```

### DEC-MP01-020 — Background Worker servis kimliği ve şirket kapsamı

```yaml
decision_id: DEC-MP01-020
title: "Background Worker için kullanıcıdan bağımsız servis kimliği"
status: approved
authority: "KaguLtd repository sahibi — önerilen güvenli model sonrası devam talimatı"
owner_role: "Ürün sahibi + güvenlik sahibi + teknik lider"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Background Worker bir insan kullanıcıyı taklit etmez. Her Worker örneği ayrı, aktif bir service identity ile tek tenant'a bağlanır. Çalışabileceği şirketler hem deployment allow-list'inde hem ERP IAM service-identity permission kaydında açıkça bulunmalıdır; etkin kapsam bu iki listenin kesişimidir ve eksik eşleşme fail-closed olur. Rapor yenileme için ayrı reporting.party-account.refresh permission'ı kullanılır."
source_or_evidence: "2026-08-30 kullanıcı devam talimatı; AGENTS.md tenant/scope kuralları; teknik temel background-job sınırı"
effective_from: 2026-08-30
review_due: 2027-06-30
blocks: [production-service-identity-provisioning, production-secret-rotation]
affected_requirements: [IAM, RPT, SEC, OPS]
notes: "Service identity insan onay quorum'una katılamaz. Sınırsız system scope yoktur. Kimlik/permission her claim öncesi ve mali projection publish öncesi yeniden doğrulanır; iptal sonrası yeni yayın fail-closed kalır. Production kimlik ve secret oluşturma bu kararın parçası değildir."
```

### DEC-MP01-021 — Açılış bakiyesi vade ve open-item davranışı

```yaml
decision_id: DEC-MP01-021
title: "Açılış bakiyesini vadeli ve allocation yapılabilir açık kalem olarak taşıma"
status: approved
authority: "KaguLtd repository sahibi — önerilen model açıkça kabul edildi"
owner_role: "Ürün sahibi + muhasebe sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Yeni açılış bakiyesi bir veya birden fazla immutable vade satırı taşır. Her satırın tutarı, vade tarihi ve payment-term snapshot kimliği/sürümü zorunludur; toplamları opening source tutarına exact eşittir. Tek toplam biliniyorsa tek satır kullanılabilir. Kalemler normal due open item gibi kısmi/tam allocation alır ve otomatik öneride oldest-due sırasına katılır. UI effective date'i vade için varsayılan önerebilir fakat kullanıcı açıkça onaylar."
source_or_evidence: "2026-08-31 kullanıcı kararı"
effective_from: 2026-08-31
review_due: 2027-06-30
blocks: []
affected_requirements: [PARTY, RPT, GL]
notes: "Legacy opening olaylarına due date uydurulmaz. Yeni settleable opening yalnız PartyAccount'ın doğal receivable/payable yönünde oluşturulur; aksi yön correction/reversal akışıdır."
```

### DEC-MP01-022 — Payment, transit ve banka mutabakatı

```yaml
decision_id: DEC-MP01-022
title: "Ödeme/tahsilat posting ve banka mutabakat anı"
status: approved
authority: "KaguLtd repository sahibi — önerilen model açıkça kabul edildi"
owner_role: "Muhasebe sahibi + finans sahibi"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Yetkili kullanıcı tarafından kesinleştirilen payment/receipt ayrı ekonomik olay ve idempotent GL sonucu üretir. Banka kesinleşmesi beklenen hareket transit/outstanding hesap üzerinden post edilir; statement reconciliation yeni payment yaratmadan mevcut hareketi doğrular ve transit hesabı banka ana hesabına kapatır. Eşleşmeyen statement satırı yalnız proposal'dır. Tolerans ilk sürümde 0,00'dır; masraf, faiz, iade ve chargeback ayrı source event'tir. Kasa hareketinde banka transit hesabı kullanılmaz."
source_or_evidence: "2026-08-31 kullanıcı kararı"
effective_from: 2026-08-31
review_due: 2027-06-30
blocks: [production-bank-account-mapping, production-bank-provider]
affected_requirements: [TRY, GL, PARTY, WF]
notes: "Payment hazırlayan final kesinleştiren/onaylayan olamaz; farklı tek yetkili yönetici yeterlidir. Allocation ve reconciliation payment lifecycle alanı değildir."
```

### DEC-MP01-023 — Party raporu manuel yenileme

```yaml
decision_id: DEC-MP01-023
title: "İlk sürüm Party projection refresh tetikleyicisi"
status: approved
authority: "KaguLtd repository sahibi — önerilen model açıkça kabul edildi"
owner_role: "Ürün sahibi + teknik lider"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "İlk sürümde reporting.party-account.refresh yetkili kullanıcı komutu idempotent biçimde durable Worker kuyruğuna occurrence ekler. Günlük/haftalık/aylık recurring schedule authoring sonraki dilimdir. Gelecek varsayılanı Europe/Nicosia, missed-run run-once ve ilk sürümde resmi-tatil kaydırması yoktur."
source_or_evidence: "2026-08-31 kullanıcı kararı"
effective_from: 2026-08-31
review_due: 2027-06-30
blocks: []
affected_requirements: [RPT, IAM, API]
notes: "Komut projection'ı request thread'inde üretmez; yalnız versioned ve deterministic iş payload'ını kuyruğa yazar."
```

### DEC-MP01-024 — MP odaklı test kadansı

```yaml
decision_id: DEC-MP01-024
title: "Dar dilim testi ve MP sonu birleşik regresyon"
status: approved
authority: "KaguLtd repository sahibi"
owner_role: "Kullanıcı + teknik lider"
owner_name: "İsim paylaşılmadı; DEC-MP01-019"
decision: "Her geliştirme oturumundan sonra tüm repository testleri tekrarlanmaz. Davranış değişikliğinde yalnız değişen finansal/güvenlik invariantının dar unit/integration testi ve gerekli compile/static kontrol çalışır. Full PostgreSQL/RLS/concurrency/golden, solution, web, Android, migration/restore ve security regresyonu ilgili MP validating/kapanış kapısında tek büyük paket olarak çalıştırılır; bulgular aynı MP içinde düzeltilip paket yeniden çalıştırılır."
source_or_evidence: "2026-08-31 kullanıcı talimatı; AGENTS.md zorunlu risk testleri korunarak yorumlandı"
effective_from: 2026-08-31
review_due: 2027-06-30
blocks: []
affected_requirements: [TEST, REL]
notes: "Testi yalnız ertelemek için production davranışı zayıflatılmaz. Yüksek riskli değişiklik ilgili dar testi geçmeden tamamlandı sayılmaz; MP kapanışında atlanan test yoktur."
```

## 4. Definition of Ready özeti

### MP-02 — Repository ve geliştirme platformu

**Sonuç: pass for technical platform.** Aşağıdaki geri döndürülebilir işler firma kararları beklerken tamamlandı:

- bağımsız repository sınırı onaylandıktan sonra solution/klasör iskeleti;
- SDK/package version pinleme, format/lint/analyzers ve temel CI;
- local PostgreSQL/Keycloak Compose ve sentetik veri;
- migration/test harness, health/readiness, structured logging ve outbox iskeleti;
- tenant/company/RLS spike; bunun nihai firma topolojisi olmadığı açıkça belirtilir;
- local backup/restore smoke.

Repository sınırı `DEC-MP01-018` ile çözülmüştür. MP-02 teknik kapıları commit `2f4d4ee` için GitHub Actions run `32360372748` dahil yerel ve remote kanıtlarla geçmiştir. Kullanıcı `DEC-MP01-019` ile isimli sahip atamasını geliştirme sonuna erteleyerek teknik faz kapanışını kabul etmiştir. `DEC-MP01-017` production hedefleri, dış servis, uzak backup ve gerçek veri için hâlâ zorunludur.

### MP-03 — Muhasebe çekirdeği ve cari ilk dikey dilim

**Sonuç: conditional pass for decision-backed slices.** `DEC-MP01-001`–`010`, `012` ve `021`–`024` ürün kararıyla geliştirme için onaylandı. Cari hesap, dönem, para, posting, banka mutabakatı, yetki, opening-aging ve Party report refresh dilimleri gerçek politika davranışıyla ilerleyebilir. İsimli uzman kabulü, resmî/production kararları ve MP kapanış test paketi kendi kapsamlarını bloklamaya devam eder.

## 5. Karar verme ve değişiklik kuralları

- Yeni cevap önce kanıt referansıyla `evidence-received` olur; etki analizi sonrası yetkili sahip `approved` yapar.
- Karar değişirse eski satır silinmez; yeni sürüm/karar eskiyi `superseded` yapar.
- Mevzuat kararı ayrıca `docs/legal/` kaydına; mimari sapma ADR'ye; faz kapısı kanıtı `MASTER_PLAN.md` dosyasına yazılır.
- Firma politikası kod içine sabit değer olarak gömülmez; uygun yerde tarih etkili/sürümlü yapılandırma olur.
