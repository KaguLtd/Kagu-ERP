# Belge İndeksi

Bu indeks, Codex'in bir görev için yalnız gerekli belgeleri yüklemesini sağlar. Önce root [MASTER_PLAN.md](../MASTER_PLAN.md) içinden program fazı, risk sınıfı ve görev yönlendirmesi belirlenir; ardından tüm dokümanları bağlama eklemek yerine aşağıdaki rota kullanılır.

## Temel belgeler

| Dosya | Ne zaman okunur |
|---|---|
| [00 Teknik temel](00-foundation/00-technical-foundation.md) | Mimari, server, teknoloji veya yeni modül işi |
| [01 Ürün kapsamı](00-foundation/01-product-scope-and-principles.md) | Kapsam/öncelik/non-goal kararı |
| [02 Domain sözlüğü](00-foundation/02-domain-glossary.md) | Her domain işi; terim belirsizliği |
| [03 Repository yapısı](00-foundation/03-repository-and-code-structure.md) | Yeni proje/klasör/katman oluşturma |
| [04 Veri mimarisi](00-foundation/04-data-architecture.md) | Tablo, migration, para, tenant, audit |
| [05 API standardı](00-foundation/05-api-contracts.md) | Endpoint, istemci, hata, idempotency |
| [06 Mimari kararlar](00-foundation/06-architecture-decisions.md) | Tasarım alternatifi veya ADR gereksinimi |
| [07 Ortak iş akışları](00-foundation/07-cross-cutting-workflows.md) | Belge state, posting, onay, dönem |
| [Araştırma kaynakları](references/SOURCES.md) | ERP/AIS kitabı, standart veya açık kaynak uygulama davranışı doğrulama |

## Modüller

| Kod | Belge | Sahip olduğu gerçek |
|---|---|---|
| IAM | [Kimlik ve yetki](modules/01-identity-access.md) | Kullanıcı, rol, permission, scope, oturum |
| ORG | [Organizasyon ve ana veriler](modules/02-organization-master-data.md) | Tenant, şirket, şube, dönem, depo ortak referansları |
| PARTY | [Cari hesaplar](modules/03-party-current-accounts.md) | Müşteri/tedarikçi, açık kalem, settlement, risk |
| INV | [Stok ve malzeme](modules/04-items-inventory.md) | Ürün, depo, hareket, rezervasyon, maliyet |
| SALES | [Satış](modules/05-sales.md) | Teklif, sipariş, sevk, fatura, iade |
| PUR | [Satın alma](modules/06-purchasing.md) | Talep, teklif, sipariş, mal kabul, üçlü eşleştirme |
| TRY | [Banka ve kasa](modules/07-banking-cash.md) | Hesap, ödeme, tahsilat, ekstre, mutabakat |
| INS | [Çek ve senet](modules/08-cheques-promissory-notes.md) | Kıymetli evrak ve değişmez durum olayları |
| GL | [Muhasebe](modules/09-accounting-general-ledger.md) | Hesap planı, posting, fiş, kapanış, mali tablolar |
| TAX | [KKTC vergi uyumu](modules/10-kktc-tax-compliance.md) | Tarih etkili vergi kuralları ve beyan çalışma tabloları |
| EINV | [KKTC e-Fatura](modules/11-kktc-e-invoice.md) | UBL-KKTC, numara, doğrulama, gönderim, iptal, arşiv |
| WF | [İş akışı ve onay](modules/12-workflow-approvals.md) | Maker-checker, limit, görev ve delegation |
| DOC | [Belge, audit, bildirim](modules/13-documents-audit-notifications.md) | Ekler, yasal arşiv, audit ve bildirim |
| RPT | [Raporlama](modules/14-reporting-dashboard.md) | Operasyonel raporlar, dashboard ve read model |
| INT | [Entegrasyonlar](modules/15-integrations.md) | Outbox/inbox ve dış sistem adaptörleri |

## İstemciler ve deneyim

- [Web geliştirme](clients/01-web-application.md)
- [Android geliştirme](clients/02-android-application.md)
- [UI/UX ve shadcn tabanlı tasarım sistemi](clients/03-ui-ux-design-system.md)

## Kalite ve operasyon

- [Test stratejisi](quality/01-testing-and-quality-strategy.md)
- [Güvenlik ve tehdit modeli](quality/02-security-and-threat-model.md)
- [Performans ve kapasite](quality/03-performance-and-capacity.md)
- [Veri migrasyonu](quality/04-data-migration-and-quality.md)
- [Yayın ve kabul](quality/05-release-and-acceptance.md)
- [Linux server dağıtımı](operations/01-linux-server-deployment.md)
- [Yedekleme, restore ve felaket kurtarma](operations/02-backup-restore-disaster-recovery.md)
- [Gözlemlenebilirlik ve olay yönetimi](operations/03-observability-and-incident-response.md)
- [Bakım ve yükseltme](operations/04-maintenance-and-upgrades.md)

## Proje ve hukuk

- [Master plan ve çalışma stratejisi](../MASTER_PLAN.md)
- [Codex geliştirme iş akışı](project/01-codex-development-workflow.md)
- [Yol haritası](project/02-delivery-roadmap.md)
- [Definition of Done](project/03-definition-of-done.md)
- [MP-01 firma politikaları karar kaydı](project/04-mp01-decision-register.md)
- [Logo ERP ve benzer ürünler benchmark'ı](references/ERP_BENCHMARK_AND_LOGO_GAP.md)
- [KKTC mevzuat matrisi](legal/01-kktc-legal-matrix.md)
- [Resmi onaylar ve açık sorular](legal/02-official-approvals-and-open-questions.md)
- [Kaynakça](references/SOURCES.md)

Araştırma okuma yolu: önce [kaynakça](references/SOURCES.md) içindeki birincil kaynağı, sonra [benchmark/boşluk analizindeki](references/ERP_BENCHMARK_AND_LOGO_GAP.md) kabul veya red gerekçesini, son olarak davranışın sahibi olan modül belgesini okuyun. Üçüncü taraf ürün davranışı doğrudan gereksinim sayılmaz.

## Requirement kimlikleri

Yeni gereksinimler şu öneklerle yazılır: `ARCH`, `DATA`, `API`, `IAM`, `ORG`, `PARTY`, `INV`, `SALES`, `PUR`, `TRY`, `INS`, `GL`, `TAX`, `EINV`, `WF`, `DOC`, `RPT`, `INT`, `WEB`, `AND`, `UX`, `SEC`, `TEST`, `OPS`, `DR`, `PERF`, `MIG`, `REL`.

Kimlik bir kez yayımlandıktan sonra başka anlama taşınmaz. Kaldırılan gereksinim `deprecated` olarak işaretlenir.
