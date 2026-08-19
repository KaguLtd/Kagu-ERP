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
| DEC-MP01-001 | Tenant ve tüzel şirket sayısı; verinin ortak/ayrı sınırı | Ürün + teknik | open | MP-03 ve production veri izolasyonu | Tek tenant/çok company destekleyen atılabilir RLS spike |
| DEC-MP01-002 | Şube, depo, kasa, banka hesabı, proje ve masraf merkezi kapsamı | Ürün + muhasebe | open | MP-03 gerçek scope ve ilgili feature'lar | Generic scope modeli ve negatif test harness tasarımı |
| DEC-MP01-003 | Mali yıl/dönem takvimi ve GL/tax/inventory/hard lock politikası | Muhasebe | open | MP-03 posting/kapanış | Sürümlü `PeriodLock` contract ve fake policy |
| DEC-MP01-004 | Fonksiyonel, işlem ve raporlama para birimleri | Muhasebe + ürün | open | MP-03 para/posting/rapor | ISO katalog, decimal value object ve string JSON contract |
| DEC-MP01-005 | Kur kaynağı, rate type, onay ve override politikası | Muhasebe | open | Çok dövizli MP-03 ve ilgili feature | Adapter portu ve manuel taslak/onay state'i |
| DEC-MP01-006 | Yuvarlama, scale ve residual politikası | Muhasebe | open | MP-03 golden mali senaryo | Parametreli decimal/rounding policy contract |
| DEC-MP01-007 | KKTC hesap planı başlangıç sürümü ve şirket alt hesap politikası | Muhasebe | open | MP-03 gerçek posting | Versioned chart import contract; sentetik hesap fixture'ı |
| DEC-MP01-008 | Posting rule, manual journal, reversal/correction ve repost onay politikası | Muhasebe + güvenlik | open | MP-03 accounting kernel | Invariant ve state-machine iskeleti; hesap kodu hard-code edilmez |
| DEC-MP01-009 | Cari vade, taksit, allocation/unallocation, avans/fazla ödeme ve write-off | Muhasebe + ürün | open | MP-03 cari/tahsilat golden senaryo | Generic açık kalem/allocation domain spike |
| DEC-MP01-010 | Banka/tahsilat/payment ile reconciliation tetikleyicileri ve transit hesaplar | Muhasebe + finans | open | MP-03 banka mutabakatı | Provider bağımsız statement/reconciliation contract |
| DEC-MP01-011 | Stok değerleme, eksi stok, backdate/repost ve sayım politikası | Muhasebe + ürün | open | MP-04 stok/satış | Generic quantity invariants ve impact-preview contract |
| DEC-MP01-012 | Rol kataloğu, permission/scope, SoD, quorum, limit ve delegation | Ürün + güvenlik | open | MP-03 onaylı gerçek akış ve production | Deny-by-default authorization iskeleti; sentetik rol fixture'ı |
| DEC-MP01-013 | KDV/tax point/beyan/düzeltme kural sahibi ve resmi yayın süreci | Muhasebe + hukuk | open | TAX/EINV feature ve production | Tarih etkili rule engine/adapter iskeleti; gerçek oran yok |
| DEC-MP01-014 | e-Fatura portal/doğrudan entegrasyon, numara, imza, retry, iptal ve arşiv | Ürün + muhasebe + operasyon | open | EINV production | Fake/portal adapter contract; gerçek gönderim kapalı |
| DEC-MP01-015 | Kişisel veri sınıfları, saklama, legal hold ve dış transfer | Güvenlik/veri | open | production ve dış servis seçimi | Local/sentetik veriyle geliştirme; dış aktarım kapalı |
| DEC-MP01-016 | Banka formatları, ödeme entegrasyonu, credential ve mutabakat yetkileri | Finans + güvenlik | open | Banka entegrasyonu/production | Fake provider ve örnek sentetik dosya parser contract |
| DEC-MP01-017 | RPO, RTO, backup lokasyonu, bakım penceresi ve on-call | Operasyon + ürün | open | MP-02 çıkış kapısının restore hedefi ve production | Local restore smoke; hedefler şartnamedeki öneri olarak etiketli |
| DEC-MP01-018 | Git repository sınırı: bağımsız `Kagu ERP` repository'si veya üst monorepo | Teknik + ürün | approved | — | Bağımsız repository, `main` dalı ve GitHub `origin` doğrulandı |

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

## 4. Definition of Ready özeti

### MP-02 — Repository ve geliştirme platformu

**Sonuç: conditional.** Aşağıdaki geri döndürülebilir işler firma kararları beklerken yapılabilir:

- bağımsız repository sınırı onaylandıktan sonra solution/klasör iskeleti;
- SDK/package version pinleme, format/lint/analyzers ve temel CI;
- local PostgreSQL/Keycloak Compose ve sentetik veri;
- migration/test harness, health/readiness, structured logging ve outbox iskeleti;
- tenant/company/RLS spike; bunun nihai firma topolojisi olmadığı açıkça belirtilir;
- local backup/restore smoke.

Repository sınırı `DEC-MP01-018` ile çözülmüştür. MP-02 repository-bootstrap görev planı hazırlanabilir. Dış servis, uzak backup veya gerçek veri için ilgili diğer kararlar yine gereklidir.

### MP-03 — Muhasebe çekirdeği ve cari ilk dikey dilim

**Sonuç: blocked for business implementation.** Teknik spike yapılabilir; fakat en az `DEC-MP01-001`–`010`, `012` ve ilgili isimli sahipler `approved` olmadan gerçek accounting-kernel davranışı, golden expected sonuçları veya MP-03 çıkış kapısı uygulanmış sayılmaz.

## 5. Karar verme ve değişiklik kuralları

- Yeni cevap önce kanıt referansıyla `evidence-received` olur; etki analizi sonrası yetkili sahip `approved` yapar.
- Karar değişirse eski satır silinmez; yeni sürüm/karar eskiyi `superseded` yapar.
- Mevzuat kararı ayrıca `docs/legal/` kaydına; mimari sapma ADR'ye; faz kapısı kanıtı `MASTER_PLAN.md` dosyasına yazılır.
- Firma politikası kod içine sabit değer olarak gömülmez; uygun yerde tarih etkili/sürümlü yapılandırma olur.
