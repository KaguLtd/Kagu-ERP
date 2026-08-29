# Resmi ve Teknik Kaynaklar

Araştırma kesim tarihi: **19 Ağustos 2026**. Teknik sorularda birincil/resmi belgeler; KKTC mevzuatında ilgili kurum sayfaları kullanıldı. Açık kaynak ERP kodu yalnız davranış ve veri modeli karşılaştırması için incelendi; lisanslı kod kopyalanmayacaktır. Kaynaklar uygulama veya release anında yeniden kontrol edilmelidir.

## 1. KKTC resmi kaynakları

### Gelir ve Vergi Dairesi

- [27/1977 Vergi Usul Yasası — değiştirilmiş ve birleştirilmiş resmi PDF](https://mevzuat.gov.ct.tr/Portals/48/27-1977%20VERGI%20USUL%20YASASI.pdf) — madde 114 normal hesap dönemini takvim yılı olarak tanımlar; özel on iki aylık dönem Vergi Dairesi kararına bağlıdır. Madde 115 muhasebe usulü ve olası tekdüzen düzenleme yetkisini açıklar.
- [KKTC Tekdüzen Hesap Planı](https://www.vergi.gov.ct.tr/?q=content%2Fkktc-tekd%C3%BCzen-hesap-plani) — hesap planı resmi yayın noktası.
- [Tekdüzen muhasebe yazılımında yetkili bilgisayar programcıları](https://www.vergi.gov.ct.tr/?q=content%2Ftekd%C3%BCzen-muhasebe-yaz%C4%B1l%C4%B1m%C4%B1nda-yetkili-bilgisayar-programc%C4%B1lar%C4%B1) — yazılım/programcı yetkilendirmesi için başlangıç kaynağı.
- [E-fatura yardım dosyaları ve kılavuzlar](https://efatura.vergi.gov.ct.tr/?q=content%2Fyard%C4%B1m-dosyalar%C4%B1-ve-klavuzlar) — teknik şema ve rehberlerin resmi yayın noktası.
- [Elektronik Fatura Düzenlenmesine İlişkin Kurallar Tüzüğü — 26.06.2026 birleştirilmiş PDF](https://mevzuat.gov.ct.tr/Portals/48/Elektronik%20Fatura%20Duzenlenmesine%20Iliskin%20Kurallar%20Tuzugu26_06_26.pdf) — kapsam, numara, entegrasyon, iptal, arşiv ve veri kaybı/bozulması kuralları.
- [Gelir ve Vergi Dairesi ana sayfası](https://www.vergi.gov.ct.tr/) — güncel duyuru/mevzuat kontrol noktası.

### Kişisel veriler

- [KKTC Kişisel Verileri Koruma Kurulu — Mevzuat](https://kvkk.gov.ct.tr/MEVZUAT) — yürürlükteki kişisel veri mevzuatı için resmi indeks.
- [Veri transferi ruhsatı başvuru duyurusu/formu](https://kvkk.gov.ct.tr/214rnek-ver%C4%B0-transfer%C4%B0-ruhsati-ba%C5%9Fvuru-formu) — yurt dışına veri transferi kurumları için başvuru bilgisi; sayfada başvuruların 9 Eylül 2024'ten itibaren kabulü duyurulmuştur.

### Çek ve bankacılık

- [KKTC Merkez Bankası — Çek Yasası/mevzuat dizini](https://www.kktcmerkezbankasi.org/tr/taxonomy/term/142) — çek süreçleri için resmi başlangıç noktası.

## 2. ERP ürün benchmark kaynakları

- [Logo Bulut ERP](https://www.logo.com.tr/urun/logo-bulut-erp) — resmi paket, işlev, eklenti ve dağıtım bilgileri.
- [Logo Tiger Wings Enterprise](https://www.logo.com.tr/urun/logo-tiger-wings-enterprise) — enterprise süreç ve web/on-premise ürün bilgileri.
- [Logo ERP Mobil ürünleri](https://www.logo.com.tr/erp-mobil-urunler) — seçilmiş mobil ERP görevleri.
- [Logo Netsis ERP ürün ailesi](https://www.logo.com.tr/lp/netsis-urunleri) — orta/büyük ve üretim odaklı ürün ailesi.
- [Mikro Jump ERP](https://www.mikro.com.tr/mikro-jump/) — resmi ürün özellikleri.
- [Odoo 19 External JSON-2 API](https://www.odoo.com/documentation/19.0/developer/reference/external_api.html) — resmi dış API ve erişim davranışı.
- [Odoo multi-company](https://www.odoo.com/documentation/19.0/applications/general/companies/multi_company.html) — resmi çok şirket kullanım modeli.
- [ERPNext integration documentation](https://docs.frappe.io/erpnext/integrating-erpnext-with-other-applications) — resmi REST/entegrasyon başlangıç noktası.
- [Microsoft Dynamics 365 Business Central](https://learn.microsoft.com/en-us/dynamics365/business-central/) — resmi ürün dokümantasyonu.
- [Business Central API v2.0](https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/api-reference/v2.0/) — resmi API referansı.

## 3. Açık kaynak ERP ve muhasebe referans uygulamaları

Bu depolar üretim bağımlılığı değildir. Kaynak belge–alt defter–büyük defter ayrımı, açık kalem kapama, stok değerleme, mutabakat, yetki ve rapor davranışlarını karşılaştırmak için seçilmiş, olgun ve kamuya açık örneklerdir.

### ERPNext / Frappe

- [ERPNext kaynak kodu](https://github.com/frappe/erpnext) — satış, satın alma, stok, muhasebe ve ödeme alt sistemlerinin açık kaynak referansı.
- [Immutable Ledger](https://docs.frappe.io/erpnext/immutable-ledger-in-erpnext) — iptal sırasında eski defter hareketini silmek yerine karşı hareket üretme ve geçmiş tarihli stok değerleme etkisi.
- [Payment Ledger](https://docs.frappe.io/erpnext/payment_ledger) ve [Payment Reconciliation](https://docs.frappe.io/erpnext/payment-reconciliation) — ödeme olayı ile faturaya tahsis/kapama ilişkisinin ayrı izlenmesi.
- [Stock Ledger](https://docs.frappe.io/erpnext/stock-ledger) — voucher kaynaklı miktar, değer ve değerleme oranı hareketleri.
- [Repost Accounting Ledger](https://docs.frappe.io/erpnext/accounting/tools/repost-accounting-ledger) — kaynak belge doğruyken türetilmiş muhasebe görünümünün kontrollü yeniden kurulması.
- [Accounts modülü teknik özeti](https://github.com/frappe/erpnext/blob/develop/erpnext/accounts/README.md) ve [Purchase Invoice posting kodu](https://github.com/frappe/erpnext/blob/develop/erpnext/accounts/doctype/purchase_invoice/purchase_invoice.py) — GL, stok ve ödeme defteri bağlantılarının kod seviyesinde karşılaştırması.

### Odoo

- [Ödeme yaşam döngüsü](https://www.odoo.com/documentation/19.0/applications/finance/accounting/payments.html) — ödemenin kaydı, “in payment” aşaması ve banka mutabakatının ayrılması.
- [Ödeme koşulları ve taksitler](https://www.odoo.com/documentation/19.0/applications/finance/accounting/customer_invoices/payment_terms.html) — her vade için ayrı açık kalem ve yaşlandırma.
- [Backend security](https://www.odoo.com/documentation/19.0/developer/reference/backend/security.html) — model, kayıt, alan ve yöntem seviyeli yetki kontrolleri.
- [Inventory valuation](https://www.odoo.com/documentation/19.0/applications/finance/accounting/get_started/inventory_valuation.html) — sürekli/dönemsel değerleme ve teslim–fatura zaman farkı tahakkukları.
- [Satın almada fatura kontrolü ve üç yönlü eşleştirme](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/purchase/manage_deals/control_bills.html).
- [Sayım](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/warehouses_storage/inventory_management/count_products.html) ve [cycle count](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/warehouses_storage/inventory_management/cycle_counts.html) — kör sayım, atama, tekrar sayım ve eşzamanlı hareket kontrolü.
- [Yıl sonu ve kilit tarihleri](https://www.odoo.com/documentation/19.0/applications/finance/accounting/reporting/year_end.html), [vergi beyanı kilidi](https://www.odoo.com/documentation/19.0/applications/finance/accounting/reporting/tax_returns.html) ve [mali rapor kataloğu](https://www.odoo.com/documentation/19.0/applications/finance/accounting/reporting.html).

### Tryton, Apache OFBiz ve iDempiere

- [Tryton kaynak kodu](https://github.com/tryton/tryton), [erişim hakları](https://docs.tryton.org/latest/server/topics/access_rights.html), [satış yaşam döngüsü](https://docs.tryton.org/7.2/modules-sale/index.html) ve [rapor altyapısı](https://docs.tryton.org/6.8/server/topics/reports/index.html) — kayıt/alan/buton kuralları, farklı kullanıcı sayısı isteyen onaylar ve sevk/fatura istisna durumları.
- [Apache OFBiz kaynak kodu](https://github.com/apache/ofbiz-framework), [Accounting entity modeli](https://raw.githubusercontent.com/apache/ofbiz-framework/trunk/applications/datamodel/entitydef/accounting-entitymodel.xml), [Order entity modeli](https://raw.githubusercontent.com/apache/ofbiz-framework/trunk/applications/datamodel/entitydef/order-entitymodel.xml) ve [Product/Inventory entity modeli](https://raw.githubusercontent.com/apache/ofbiz-framework/trunk/applications/datamodel/entitydef/product-entitymodel.xml) — party-role, payment application, facility/location, sahiplik ve sipariş düzeltmeleri için veri modeli karşılaştırması.
- [OFBiz account reconciliation](https://cwiki.apache.org/confluence/display/OFBENDUSER/12.2.3%2BAccount%2BReconciliations) — mutabakat seti ve durum davranışı.
- [iDempiere kaynak kodu](https://github.com/idempiere/idempiere) — accounting schema, posting motoru, Fact_Acct, allocation ve boyutlu muhasebe yaklaşımının karşılaştırma kaynağı.

### Küçük/orta işletme muhasebesi

- [LedgerSMB kaynak kodu](https://github.com/ledgersmb/LedgerSMB), [açık LedgerSMB kitabı](https://github.com/ledgersmb/ledgersmb-book) ve [reconciliation controller belgesi](https://docs.ledgersmb.org/perl-api/1.11.7/LedgerSMB/Scripts/recon.pm.html) — PostgreSQL tabanlı çift kayıt, rapor şablonları ve submit/approve/reject mutabakat akışı.
- [GnuCash kaynak kodu](https://github.com/Gnucash/gnucash), [lots belgesi](https://www.gnucash.org/docs/v5/C/gnucash-manual/tool-lots.html) ve [invoice belgesi](https://www.gnucash.org/docs/v5/C/gnucash-manual/busnss-ar-invoices1.html) — işlem/split modeli ile ödeme veya alacak dekontunu faturaya bağlayan lot yaklaşımı.
- [Dolibarr kaynak kodu](https://github.com/Dolibarr/dolibarr) ve [çift kayıt modülü ayrımı](https://wiki.dolibarr.org/index.php/Module_Accounting) — basit ön muhasebe ile gerçek çift kayıt muhasebenin aynı şey olmadığını gösteren kapsam karşılaştırması.
- [Ledger CLI](https://github.com/ledger/ledger) — bakiyenin değiştirilebilir bir alan değil, doğrulanabilir çift kayıt hareketlerinden türetilmesi için sade referans.

## 4. Kitaplar, açık dersler ve akademik çalışmalar

### Muhasebe bilgi sistemleri ve veri modeli

- [OpenStax Principles of Financial Accounting, Bölüm 7](https://openstax.org/books/principles-financial-accounting/pages/7-why-it-matters) ve [special journals/subsidiary ledgers](https://openstax.org/books/principles-financial-accounting/pages/7-2-describe-and-explain-the-purpose-of-special-journals-and-their-importance-to-stakeholders) — satış, tahsilat, ödeme, satın alma günlükleri; alt defter ve kontrol hesabı mutabakatı.
- [Open Financial and Managerial Accounting](https://open.umn.edu/opentextbooks/textbooks/1659) — muhasebe döngüsü, iç kontrol, alacak, stok, nakit akış ve maliyet muhasebesi için açık ders kitabı.
- [ANU Accounting Information Systems dersi](https://programsandcourses.anu.edu.au/2020/course/infs2005/first%20semester/2433) — gelir, harcama, büyük defter/raporlama, sistem belgeleme ve iç kontrol döngülerinin öğretim sırası.
- [Romney, Steinbart, Summers ve Wood — Accounting Information Systems](https://www.pearson.com/en-us/subject-catalog/p/accounting-information-systems/P200000010389/9780138114411) — AIS, süreç, kontrol ve teknoloji konuları için ders kitabı referansı; telifli olduğundan yalnız kavramsal karşılaştırma yapılır.
- [Silverston ve Simsion — The Data Model Resource Book, Volume 1](https://books.wiley.com/authors/len-silverston/vol-1-book/) — party, order, shipment, invoice, accounting ve bütçe için evrensel veri modeli desenleri.
- [McCarthy — Resources, Events, Agents kaynakları](https://www.williamemccarthy.com/), [REA Design Theory](https://aisel.aisnet.org/cais/vol38/iss1/29/) ve [OOREA](https://aisel.aisnet.org/icis2004/16/) — kaynak, ekonomik olay, taahhüt ve iç/dış aktörleri journal satırlarından önce modelleme.
- [CSU Sacramento — Revenue Cycle AIS Design](https://scholars.csus.edu/esploro/outputs/graduate/Accounting-information-system-design-for-a/99257831031501671) — REA/DFD’den ilişkisel şema, form ve rapora ilerleyen tasarım örneği.

### Yazılım mimarisi ve muhasebe kalıpları

- [Fowler — Patterns of Enterprise Application Architecture](https://martinfowler.com/books/eaa.html) ve [pattern kataloğu](https://martinfowler.com/eaaCatalog/) — domain, transaction, data mapping, concurrency ve dağıtım kalıpları.
- Fowler’ın [Accounting Entry](https://martinfowler.com/eaaDev/AccountingEntry.html), [Accounting Transaction](https://martinfowler.com/eaaDev/AccountingTransaction.html), [Accounting Narrative](https://martinfowler.com/eaaDev/AccountingNarrative.html), [Account](https://martinfowler.com/eaaDev/Account.html) ve [Audit Log](https://martinfowler.com/eaaDev/AuditLog.html) taslakları — çok bacaklı dengeli işlem, booking sonrası değişmezlik, düzeltme ve olay–muhasebe izi.
- [Evans — Domain-Driven Design](https://www.informit.com/store/domain-driven-design-tackling-complexity-in-the-heart-9780321125217) — ortak dil, bounded context ve model ile kodun birlikte evrilmesi.

### ERP uygulama ve benimseme

- [Markus, Tanis ve van Fenema — Learning from Adopters’ Experiences with ERP](https://journals.sagepub.com/doi/pdf/10.1177/026839620001500402) — başarıyı yalnız go-live anında değil, uygulama yaşam döngüsü boyunca ölçme.
- [METU — ERP implementation methodologies](https://open.metu.edu.tr/handle/11511/12155) ve [ERP’yi şirket süreçlerine yerleştirme](https://open.metu.edu.tr/handle/11511/14128) — ön hazırlık, uygulama ve uygulama sonrası gelişim.
- [Open University — ERP implementation assessment framework](https://oro.open.ac.uk/90299/) — kritik başarı faktörlerini uygulama boyunca ölçme.
- [User participation review](https://nru.uncst.go.ug/handle/123456789/6546) — süreçler arası ERP değişiminde kullanıcı katılımı ve direnç riski.

## 5. Süreç, belge, banka ve iç kontrol standartları

- [OASIS UBL 2.3](https://docs.oasis-open.org/ubl/UBL-2.3.html) — Order, Despatch Advice, Receipt Advice, Invoice, Credit Note ve Remittance Advice gibi belge tipleri ile satır referansları.
- [OMG BPMN 2.0.2](https://www.omg.org/spec/BPMN/) — iş akışlarını olay, görev, gateway, swimlane ve hata/telafi davranışıyla belgeleme standardı.
- [ISO 20022 mesaj arşivi](https://www.iso20022.org/catalogue-messages/iso-20022-messages-archive) — camt.052 hesap raporu, camt.053 hesap ekstresi ve camt.054 borç/alacak bildirimi şemaları.
- [COSO Internal Control—Integrated Framework](https://www.coso.org/internal-control) — kontrol ortamı, risk değerlendirme, kontrol faaliyetleri, bilgi/iletişim ve izleme eksenleri.
- [COSO Fraud Risk Management](https://www.coso.org/frauddeterrence) — görev ayrılığına ek olarak hile risk değerlendirmesi, veri analitiği ve bildirim mekanizması.
- [AWS Transactional Outbox](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/transactional-outbox.html) — iş değişikliği ve outbox kaydını aynı transaction’da yazma, sıra ve idempotent consumer gereksinimi.

## 6. Platform ve backend

- [Microsoft .NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy) — .NET 10 LTS durumu ve destek takvimi; araştırma tarihinde aktif LTS, 14 Kasım 2028'e kadar destekli.
- [PostgreSQL Versioning Policy](https://www.postgresql.org/support/versioning/) — araştırma tarihinde PostgreSQL 18 güncel desteklenen major; 14 Kasım 2030'a kadar destek ve güncel minor kullanma politikası.
- [PostgreSQL 18 Row Security Policies](https://www.postgresql.org/docs/18/ddl-rowsecurity.html) — RLS etkinliği, default-deny ve policy davranışı.
- [PostgreSQL 18 Continuous Archiving and PITR](https://www.postgresql.org/docs/18/continuous-archiving.html) — WAL arşivi ve point-in-time recovery.
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/) — trace, metric ve log enstrümantasyonu.

## 7. Web ve UI

- [shadcn/ui — Vite installation](https://ui.shadcn.com/docs/installation/vite) — Vite tabanı, sahip olunan bileşen yaklaşımı ve güncel kurulum.
- [Tailwind CSS — installation](https://tailwindcss.com/docs/installation/tailwind-cli) — resmi Tailwind kurulum belgeleri.
- [TanStack Query — React installation](https://tanstack.com/query/latest/docs/framework/react/installation) — sunucu durumu kütüphanesinin resmi belgesi.
- [W3C Web Content Accessibility Guidelines](https://www.w3.org/WAI/standards-guidelines/wcag/) — WCAG 2.2 ve erişilebilirlik standardı.
- [Playwright — best practices](https://playwright.dev/docs/best-practices) — kullanıcıya görünür davranış ve izole E2E önerileri.
- [Playwright — browsers](https://playwright.dev/docs/browsers) — tarayıcı test matrisi.

## 8. Android

- [Android architecture recommendations](https://developer.android.com/topic/architecture/recommendations) — layered architecture, repository, UDF ve lifecycle önerileri.
- [Android offline-first data layer](https://developer.android.com/topic/architecture/data-layer/offline-first) — yerel kaynak, senkronizasyon ve kuyruk yaklaşımı.
- [Android security best practices](https://developer.android.com/privacy-and-security/security-best-practices) — güvenli iletişim, credential/storage ve bileşen güvenliği.
- [Compose Material 3 Adaptive releases](https://developer.android.com/jetpack/androidx/releases/compose-material3-adaptive) — adaptive Compose bileşenleri.
- [Adaptive list-detail layout](https://developer.android.com/develop/adaptive-apps/guides/list-detail) — telefon/tablet list-detail kalıbı.

## 9. Kimlik, sunucu ve container

- [Keycloak hostname configuration](https://www.keycloak.org/server/hostname) — public URL/proxy/hostname güvenliği.
- [Keycloak containers](https://www.keycloak.org/server/containers) — optimize edilmiş production image ve container yapılandırması.
- [Keycloak production configuration](https://www.keycloak.org/server/configuration-production) — production mode ve güvenli kurulum.
- [Caddy automatic HTTPS quick start](https://caddyserver.com/docs/quick-starts/https) — HTTPS ve sertifika otomasyonu.
- [Caddy reverse proxy quick start](https://caddyserver.com/docs/quick-starts/reverse-proxy) — ters proxy resmi rehberi.
- [Docker Compose](https://docs.docker.com/compose/) — çoklu container uygulama tanımı.
- [Docker Compose production](https://docs.docker.com/compose/how-tos/production/) — üretim override ve deployment rehberi.
- [Ubuntu 26.04 LTS release notes](https://documentation.ubuntu.com/release-notes/26.04/) — önerilen yeni LTS'nin resmi sürüm/destek bilgisi.
- [Ubuntu 24.04 LTS release notes](https://documentation.ubuntu.com/release-notes/24.04/) — desteklenen operasyon fallback'i.

## 10. Backup ve güvenlik

- [pgBackRest User Guide](https://pgbackrest.org/user-guide.html) — PostgreSQL full/differential/incremental, archive ve restore.
- [restic — Restoring from backup](https://restic.readthedocs.io/en/stable/050_restore.html) — dosya/object restore ve doğrulama.
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) — ASVS 5.0 uygulama güvenliği kontrol standardı.

## 11. Codex çalışma yöntemi

- [Codex best practices](https://learn.chatgpt.com/guides/best-practices) — amaç/bağlam/kısıt/tamamlanmış tanımı, planlama ve doğrulama.
- [Codex AGENTS.md guide](https://learn.chatgpt.com/docs/agent-configuration/agents-md) — root ve iç içe talimat dosyalarının keşif/öncelik davranışı.

## 12. Kaynak kullanım ve kanıt notu

- Resmi PDF/şema/kod listeleri release artefaktında URL, indirme tarihi ve SHA-256 ile saklanmalıdır.
- Dinamik web sayfası değişebileceğinden “son doğrulama tarihi” hukuki matriste güncellenmelidir.
- Blog, forum veya Türkiye mevzuatı KKTC kuralının birincil kaynağı değildir.
- Üçüncü taraf ürün belgeleri yalnız teknik ürün davranışı için; mali/hukuki yorum için kurum/uzman kararı gerekir.
- Açık kaynak koddan yalnız davranış, invariant ve veri ilişkisi öğrenilir; kod/şema kopyalanmadan önce lisans ve clean-room incelemesi yapılır.
- Bir kaynaktaki özelliğin varlığı bu ürüne otomatik gereksinim değildir. Her kabul, KKTC iş ihtiyacı, iç kontrol, bakım maliyeti ve orta ölçekli firma kapsamıyla gerekçelendirilir.
- Her kritik karar için kaynak URL’si yanında “gözlenen davranış → projeye çıkarım → kabul/red gerekçesi → doğrulama testi” kaydı tutulur.
