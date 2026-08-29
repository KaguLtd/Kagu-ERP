# KKTC ERP — Codex Master Plan ve Çalışma Stratejisi

Bu dosya, şartname paketinin uygulamaya hangi sırayla ve hangi kanıtlarla dönüştürüleceğini yönetir. İş gereksinimlerinin yerine geçmez; doğru belgeyi doğru görevde okutmak, bağımlılık sırasını korumak, büyük işleri küçük dikey dilimlere bölmek ve Codex'in plansız genişlemesini önlemek için ana yürütme rotasıdır.

> Durum: active  
> Master plan sürümü: 1.0  
> Paket sürümü: v1.2  
> Son güncelleme: 22 Ağustos 2026
> Kapsam: Repository, backend, PostgreSQL, web, Android, güvenlik, mevzuat, test, Linux operasyonu ve felaket kurtarma  
> Birincil sahipler: Ürün sahibi, teknik lider, mali müşavir/muhasip, güvenlik ve operasyon sorumluları

## 1. Bu planın cevapladığı sorular

Codex her görevde şu soruların cevabını bu dosyadan ve yönlendirdiği belgelerden çıkarmalıdır:

1. İstek hangi program fazına ve hangi iş alanına aittir?
2. İşe başlamadan önce hangi belgeler zorunlu, hangileri yalnız koşullu olarak okunmalıdır?
3. Ön koşullar ve karar kapıları tamamlanmış mıdır?
4. Bu iş için ayrı bir yaşayan görev planı gerekir mi?
5. En küçük fakat uçtan uca doğrulanabilir dikey dilim nedir?
6. Finansal, yetki, tenant, mevzuat, migration, güvenlik ve restore etkisi nedir?
7. İşin tamamlandığını hangi test, mutabakat ve operasyon kanıtı gösterecektir?
8. Tamamlanan iş master plandaki hangi kapıyı ilerletir?

Bu soruların cevabı yoksa Codex doğrudan kod yazmaya başlamaz. Eksik bağlamı keşfeder, güvenle cevaplanamayan yüksek etkili kararı açık soru olarak bildirir.

## 2. Belge rolleri ve otorite

Belge setindeki dosyalar aynı işi yapmaz:

| Belge | Rolü |
|---|---|
| [AGENTS.md](AGENTS.md) | Her görev için kısa, bağlayıcı güvenlik ve geliştirme kuralları |
| [MASTER_PLAN.md](MASTER_PLAN.md) | Program sırası, görev yönlendirme, fazlar, kapılar ve Codex çalışma döngüsü |
| [README.md](README.md) | Paket özeti, değişmez ana kararlar ve ilk yönlendirme |
| [docs/README.md](docs/README.md) | Konuya göre ayrıntılı belge indeksi |
| Foundation belgeleri | Mimari, veri, API ve ortak süreç sözleşmeleri |
| Modül belgeleri | İş davranışı, değişmezler, durum makineleri, rapor ve kabul ölçütleri |
| Client, quality ve operations belgeleri | İstemci, test, güvenlik, yayın ve işletim sözleşmeleri |
| Legal belgeler | Resmi doğrulama gerektiren alanlar, mevzuat kaynakları ve açık kararlar |
| ADR kayıtları | Kabul edilmiş mimari kararlar ve gerekçeleri |
| [PLANS.md](PLANS.md) | Tek bir karmaşık iş için yaşayan uygulama planının biçimi |
| Görev planı | Belirli bir sonuç için dosya, milestone, test, risk ve ilerleme kaydı |

Çelişki halinde şu sıra uygulanır:

1. Güncel mevzuat ve yazılı resmi kurum cevabı.
2. AGENTS.md güvenlik, finansal bütünlük ve veri koruma kuralları.
3. Kabul edilmiş ADR.
4. Foundation veri, API ve mimari sözleşmeleri.
5. Davranışın sahibi olan modül veya istemci belgesi.
6. Bu master planın sıralama ve kalite kapıları.
7. Görev planı, issue veya geçici çalışma notu.

Master plan, alt seviye bir belgenin iş kuralını yeniden tanımlamaz. Bir uyumsuzluk bulunursa sessizce seçim yapılmaz; ilgili ADR, legal açık soru veya kullanıcı kararı istenir.

## 3. İki seviyeli planlama modeli

### 3.1 Program seviyesi

Bu dosya program seviyesindeki tek ana plandır. Şunları taşır:

- Fazların sırası ve bağımlılıkları.
- Faz giriş ve çıkış kapıları.
- Güncel faz durumu.
- İlk uygulama backlog sırası.
- Her görev türü için belge ve test yönlendirmesi.
- Codex'in varsayılan çalışma stratejisi.

### 3.2 Görev seviyesi

Belirli bir özellik, migration veya yayın için ayrıntılı plan docs/project/plans altında tutulur. Görev planı master plana bağlıdır; bağımsız bir yol haritası değildir.

Aşağıdakilerden biri varsa görev planı zorunludur:

- İşin bir günde güvenle bitmesi beklenmiyorsa.
- Birden çok modül veya istemciyi etkiliyorsa.
- Veritabanı migration'ı veya veri backfill'i içeriyorsa.
- Finansal posting, allocation, stok maliyeti, dönem veya rapor toplamını değiştiriyorsa.
- Yetki, tenant izolasyonu, kişisel veri ya da secret davranışını değiştiriyorsa.
- Mevzuat veya resmi dış entegrasyon yorumu içeriyorsa.
- Linux dağıtımı, backup, restore, felaket kurtarma veya production işlemi içeriyorsa.
- Geri dönüşü pahalıysa veya ADR gerektiriyorsa.
- Eski web ya da Android sürümleriyle API uyumluluğu riski varsa.

Küçük, düşük riskli ve tek sonuçlu görev için ayrı dosya açılmayabilir. Buna rağmen amaç, kapsam, done when, test ve risk çalışma planında açık olmalıdır.

### 3.3 Standart durumlar

Master fazları ve görev planları şu durumları kullanır:

| Durum | Anlamı |
|---|---|
| proposed | Kapsam taslak, giriş kapısı henüz kanıtlanmadı |
| ready | Ön koşullar, sahipler ve kabul ölçütleri yeterli |
| in-progress | Üzerinde aktif çalışma var |
| blocked | Belirli bir karar veya dış bağımlılık olmadan güvenle ilerleyemez |
| validating | Uygulama tamamlandı; test, UAT, restore veya mutabakat kanıtı bekleniyor |
| completed | Çıkış kapısı kanıtları kayıtlı |
| superseded | Daha yeni bir karar veya planla değiştirilmiş |

Tamamlandı ifadesi yalnız kod yazıldığı anlamına gelmez. İlgili test, veri, güvenlik, rapor, operasyon ve doküman kanıtları da mevcut olmalıdır.

## 4. Codex oturum başlangıç protokolü

Her yeni görevde veya yarım kalan göreve dönüşte Codex sırayla şunları yapar:

1. Root AGENTS.md dosyasını ve bu master planı okur.
2. İlk kez çalışıyorsa veya paket sürümü değişmişse README.md dosyasını okur.
3. Kullanıcı isteğini program fazı, domain, katman ve risk sınıfına ayırır.
4. Aşağıdaki yönlendirme matrisinden zorunlu belgeleri seçer.
5. Mevcut kodu, testleri, migration'ları, ADR'leri ve kullanıcı değişikliklerini inceler.
6. İlgili fazın giriş kapısını ve önceki bağımlılıkların durumunu kontrol eder.
7. Ayrı görev planı gerekip gerekmediğine karar verir; gerekirse PLANS.md formatını kullanır.
8. Bilinenler, varsayımlar, kapsam dışı alanlar, blokajlar ve done when maddelerini yazar.
9. En küçük uçtan uca dikey dilimi seçer.
10. Uygulama, test, inceleme, dokümantasyon ve plan güncelleme döngüsünü tamamlar.

Kullanıcı yalnız devam et derse Codex rastgele bir modül seçmez. En düşük numaralı, tamamlanmamış ve blokajsız fazın sıradaki işini ele alır. Hukuki kararlar beklerken teknik olarak geri döndürülebilir temel işler paralel ilerleyebilir; bekleyen mevzuat yorumu kod içine varsayım olarak gömülmez.

## 5. Minimum bağlam ilkesi

Her görevde bütün şartname paketini okumak doğru değildir. Codex bağlamı üç katmanda toplar:

### Katman A — Her görevde zorunlu

- AGENTS.md
- Bu master planın güncel durum, yönlendirme ve ilgili faz bölümleri
- Görevin mevcut kodu, testleri ve değişiklikleri

### Katman B — Göreve göre zorunlu

- Bir foundation belgesi
- Davranışın sahibi modül belgesi
- İlgili test, güvenlik, istemci veya operasyon belgesi
- Varsa ilgili ADR ve görev planı

### Katman C — Yalnız tetiklenirse

- Mevzuat ve resmi onay belgeleri
- Araştırma kaynakları ve ürün benchmark'ı
- Migrasyon, performans, restore veya olay yönetimi belgeleri
- Başka bir modülün sözleşmesi

Codex önce docs/README.md üzerinden rota seçer. Bir belgeyi yalnız dosya adından tahmin ederek kullanmaz; davranışın gerçekten sahibi olup olmadığını kontrol eder. Okumadığı ilgisiz belgeleri okumuş gibi göstermez.

## 6. Görevden belgeye yönlendirme matrisi

Aşağıdaki tablo minimum okuma setidir. Çapraz etki görülürse ek belge okunur.

| Görev türü | Zorunlu ana belgeler | Koşullu ek belgeler |
|---|---|---|
| Repository bootstrap veya klasörleme | docs/00-foundation/00-technical-foundation.md, docs/00-foundation/03-repository-and-code-structure.md, docs/project/01-codex-development-workflow.md | ADR, Linux dağıtımı, test stratejisi |
| Mimari veya yeni altyapı bileşeni | docs/00-foundation/00-technical-foundation.md, docs/00-foundation/06-architecture-decisions.md | Güvenlik, performans, operasyon, ilgili ADR |
| Veritabanı şeması veya migration | docs/00-foundation/04-data-architecture.md, ilgili modül, docs/quality/04-data-migration-and-quality.md | Backup/restore, release, performans |
| API endpoint veya contract | docs/00-foundation/05-api-contracts.md, ilgili modül | Web, Android, güvenlik, entegrasyon |
| Kimlik, rol, permission veya scope | docs/modules/01-identity-access.md, docs/quality/02-security-and-threat-model.md | Organizasyon, workflow, web, Android |
| Tenant, şirket, şube, dönem veya depo | docs/modules/02-organization-master-data.md, docs/00-foundation/04-data-architecture.md | IAM, muhasebe, stok |
| Cari kart, açık kalem veya allocation | docs/modules/03-party-current-accounts.md, docs/modules/09-accounting-general-ledger.md | Banka, satış, satın alma, raporlama |
| Stok kartı, hareket, rezervasyon veya maliyet | docs/modules/04-items-inventory.md, docs/00-foundation/07-cross-cutting-workflows.md | Satış, satın alma, GL, backdate testleri |
| Satış akışı | docs/modules/05-sales.md, docs/modules/03-party-current-accounts.md | Stok, GL, vergi, e-Fatura, workflow |
| Satın alma akışı | docs/modules/06-purchasing.md, docs/modules/03-party-current-accounts.md | Stok, GL, vergi, workflow |
| Banka, kasa, ödeme veya tahsilat | docs/modules/07-banking-cash.md, docs/modules/03-party-current-accounts.md | GL, entegrasyon, raporlama |
| Çek veya senet | docs/modules/08-cheques-promissory-notes.md, docs/modules/07-banking-cash.md | Cari, GL, workflow, audit |
| Hesap planı, fiş, posting veya kapanış | docs/modules/09-accounting-general-ledger.md, docs/00-foundation/04-data-architecture.md | İlgili kaynak modül, raporlama, vergi |
| KDV, stopaj veya beyan çalışma tablosu | docs/modules/10-kktc-tax-compliance.md, docs/legal/01-kktc-legal-matrix.md | GL, satış, satın alma, resmi açık sorular |
| e-Fatura veya UBL-KKTC | docs/modules/11-kktc-e-invoice.md, docs/legal/02-official-approvals-and-open-questions.md | Satış, vergi, entegrasyon, arşiv |
| Onay, maker-checker veya delegation | docs/modules/12-workflow-approvals.md, docs/modules/01-identity-access.md | İlgili iş modülü, audit |
| Dosya, audit veya bildirim | docs/modules/13-documents-audit-notifications.md | Güvenlik, retention, ilgili modül |
| Rapor, dashboard veya export | docs/modules/14-reporting-dashboard.md, ilgili kaynak modül | GL, performans, web, kişisel veri |
| Dış entegrasyon, outbox veya inbox | docs/modules/15-integrations.md, docs/00-foundation/05-api-contracts.md | Dış sistem modülü, güvenlik, operasyon |
| Web ekranı veya web akışı | docs/clients/01-web-application.md, docs/clients/03-ui-ux-design-system.md | API, IAM, ilgili modül, Playwright |
| Android ekranı, offline veya senkronizasyon | docs/clients/02-android-application.md, docs/00-foundation/05-api-contracts.md | IAM, ilgili modül, güvenlik |
| UI/UX tasarım sistemi | docs/clients/03-ui-ux-design-system.md | Web veya Android, erişilebilirlik testleri |
| Test ekleme veya hata düzeltme | docs/quality/01-testing-and-quality-strategy.md, davranış sahibi belge | Güvenlik, migration, istemci E2E |
| Tehdit, erişim veya güvenlik açığı | docs/quality/02-security-and-threat-model.md, docs/modules/01-identity-access.md | API, web, Android, operations |
| Performans veya kapasite | docs/quality/03-performance-and-capacity.md, ilgili modül | Raporlama, DB, gözlemlenebilirlik |
| Eski veriden taşıma | docs/quality/04-data-migration-and-quality.md, docs/00-foundation/04-data-architecture.md | Modüller, GL mutabakatı, restore |
| Release, UAT veya kabul | docs/quality/05-release-and-acceptance.md, docs/project/03-definition-of-done.md | Test, migration, operasyon |
| Linux kurulum veya dağıtım | docs/operations/01-linux-server-deployment.md | Güvenlik, release, bakım |
| Backup, restore veya felaket kurtarma | docs/operations/02-backup-restore-disaster-recovery.md | Linux, güvenlik, migration |
| Log, metric, trace veya incident | docs/operations/03-observability-and-incident-response.md | Güvenlik, ilgili modül, runbook |
| Upgrade veya rutin bakım | docs/operations/04-maintenance-and-upgrades.md | Release, backup/restore, ADR |
| Yeni ürün özelliği veya kapsam kararı | docs/00-foundation/01-product-scope-and-principles.md, ilgili modül | Benchmark, yol haritası, legal |
| Logo ERP ile davranış karşılaştırması | docs/references/ERP_BENCHMARK_AND_LOGO_GAP.md, docs/references/SOURCES.md | Davranış sahibi modül |
| Mevzuat yorumu veya resmi oran | docs/legal/01-kktc-legal-matrix.md, docs/legal/02-official-approvals-and-open-questions.md | Vergi, e-Fatura, GL |

Bir değişiklik birden çok satıra giriyorsa her satırın zorunlu belgesi birleştirilir; tekrarlar tek kez okunur.

## 7. Bağımlılık omurgası

Programın ana bağımlılık sırası şöyledir:

1. Yönetişim, ürün politikaları ve resmi açık sorular.
2. Repository, geliştirme ortamı, CI ve temel runtime.
3. Kimlik, tenant, şirket kapsamı ve veri izolasyonu.
4. Muhasebe çekirdeği ve dönem kuralları.
5. Cari, açık kalem, ödeme, allocation ve banka mutabakatı.
6. Stok, satış, satın alma ve çek/senet süreçleri.
7. Vergi ve e-Fatura gibi resmi entegrasyonlar.
8. Üretim kalitesinde web ve kontrollü Android kapsamı.
9. Veri migrasyonu, pilot, paralel çalışma ve kullanıcı kabulü.
10. Production, restore kanıtı, felaket kurtarma ve hypercare.

Aşağıdaki konular bütün fazları keser ve sona bırakılamaz:

- Yetki ve tenant izolasyonu.
- Audit ve değişmez finansal kayıt.
- API idempotency ve concurrency.
- Transactional outbox ve dış entegrasyon hataları.
- Otomatik test ve gerçek PostgreSQL doğrulaması.
- Gözlemlenebilirlik ve hassas veri maskeleme.
- Backup, restore ve rebuild yeteneği.
- Rapor mutabakatı ve kaynak belgeye drill-down.
- Doküman, ADR ve karar izlenebilirliği.

## 8. Program fazları ve güncel durum

| Faz | Ad | Durum | Ana çıktı | Bir sonraki kapı |
|---|---|---|---|---|
| MP-00 | Şartname ve yönlendirme tabanı | completed | v1.2 çoklu belge paketi ve master plan | Paket doğrulama kaydı |
| MP-01 | Firma politikaları ve resmi kararlar | in-progress | Sahipli karar ve açık soru kayıtları | Kritik bilinmeyenlerin sınıflandırılması |
| MP-02 | Repository ve geliştirme platformu | completed | Çalışan iskelet, CI, local Compose, auth ve DB | MP-03 teknik spike sınırı |
| MP-03 | Muhasebe çekirdeği ve cari ilk dikey dilim | in-progress | Kaynak olaydan rapora uzlaşan uçtan uca akış | Golden senaryo sıfır fark |
| MP-04 | Stok ve satış çekirdeği | proposed | Sipariş, rezervasyon, sevk, fatura, iade | Miktar ve değer mutabakatı |
| MP-05 | Satın alma, banka, kasa ve çek/senet | proposed | Kontrollü borç, nakit ve kıymetli evrak akışları | Subledger ve banka mutabakatı |
| MP-06 | KKTC vergi ve resmi e-Fatura | blocked | Sürümlü vergi kuralları ve onaylı entegrasyon | Yazılı resmi doğrulamalar |
| MP-07 | Üretim kalitesinde web istemcisi | proposed | Rol bazlı, erişilebilir, hızlı web uygulaması | Kritik akış E2E ve UAT |
| MP-08 | Android pilot istemcisi | proposed | Dar, güvenli ve ölçülebilir mobil kapsam | Cihaz, offline ve auth testleri |
| MP-09 | Veri migrasyonu, pilot ve paralel kapanış | proposed | Temizlenmiş veri ve imzalı mutabakat | Go-live onayı |
| MP-10 | Production, DR ve hypercare | proposed | İşletilebilir yayın, geri dönüş ve destek | 30/60/90 gün kabulü |

Faz durumu tahmini ilerleme yüzdesi değildir. Bir faz yalnız aşağıdaki çıkış kapısı kanıtları mevcutsa completed yapılır.

## 9. Faz kapıları ve teslimatlar

### MP-00 — Şartname ve yönlendirme tabanı

Amaç: Koddan önce ortak dil, mimari sınır, belge rotası ve kabul standardı oluşturmak.

Çıkış kapısı:

- Root kuralları, master plan, belge indeksi ve görev planı standardı birbirine bağlıdır.
- Foundation, modül, istemci, kalite, operasyon, hukuk ve referans belgeleri erişilebilirdir.
- Belge içi bağlantılar geçerlidir.
- Açık mevzuat alanları kesin gereksinim gibi gösterilmez.
- Paket tekrar üretilebilir ve arşiv bütünlüğü doğrulanmıştır.

### MP-01 — Firma politikaları ve resmi kararlar

Amaç: Yazılım davranışını değiştirecek firma ve KKTC kararlarını koddan önce sahipli hale getirmek.

Minimum karar seti:

- Tenant, şirket, şube, depo, kasa ve mali dönem yapısı.
- Kullanıcı rolleri, görev ayrılığı, onay limitleri ve vekâlet ilkeleri.
- Hesap planı başlangıç şablonu ve şirket özelleştirme politikası.
- Fonksiyonel para birimi, işlem para birimi, kur kaynağı ve yuvarlama.
- Stok değerleme yöntemi, eksi stok ve backdate politikası.
- Cari risk limiti, vade, taksit, allocation ve avans ödeme politikası.
- Çek/senet durumları, ciro, teminat, tahsil ve karşılıksız işlem politikası.
- KDV ve diğer yükümlülüklerde resmi kaynak, yürürlük tarihi ve kural sahibi.
- e-Fatura kayıt, numara, imza, gönderim, retry, iptal ve saklama sorumluluğu.
- Kişisel veri, saklama süresi, ülke dışı transfer ve backup lokasyonu.
- Banka ekstre biçimleri ve entegrasyon yetkileri.
- RPO, RTO, bakım penceresi, erişim ve olay eskalasyon sahipleri.

Çıkış kapısı:

- Her kararın sahibi, tarihi, kaynağı ve durumu vardır.
- Bilinmeyen resmi alanlar legal açık soru kaydındadır.
- Konfigürasyonla yönetilecek alan ile kod davranışı ayrılmıştır.
- Kodlamayı bloklayan ve bloklamayan belirsizlikler açıkça sınıflandırılmıştır.
- MP-02 ve MP-03 için Definition of Ready kontrolü yapılmıştır.

### MP-02 — Repository ve geliştirme platformu

Amaç: Tüm modüllerin üzerinde güvenle büyüyeceği, tek komutla kurulabilen geliştirme tabanı oluşturmak.

Durum kanıtı (21 Ağustos 2026): bağımsız repository, .NET solution, strict web workspace, Android scaffold ve CI sözleşmesi oluşturuldu. JDK 17, Android Studio/SDK ve Docker/WSL 2 doğrulandı; Android lint/unit/instrumentation derleme ile API 29 managed-emulator Compose testi, PostgreSQL/Keycloak Compose health, boş ve örnek verili migration, DB/RLS negatifleri, deny-by-default API tenant/company scope, correlation zincirli append-only authorization audit, DB-backed readiness, güvenli structured telemetry, transactional duplicate-safe outbox, gerçek Keycloak subject→ERP DB üyelik/şirket/permission, ayrı hedefe local restore smoke ve servis açmayan temiz kaynak bootstrap testleri geçti. İlk CI loglarındaki Linux path, internal Compose ağı, Android SDK komut yolu, POSIX nested-script çalıştırma ve ephemeral secret masking kusurları düzeltildi. Commit `2f4d4ee` için GitHub Actions run `32360372748` ve karar kaydı commit `51dd32c` için run `32360860976` içindeki altı job'ın tamamı geçti. Kullanıcı `DEC-MP01-019` ile isimli sahip atamalarını geliştirme sonuna erteledi ve bunun production/mali politika onayı olmadığını kabul ederek teknik ilerlemeye izin verdi. Ayrıntı ve komut kanıtı [MP-02 yaşayan görev planındadır](docs/project/plans/2026-08-19-repository-bootstrap.md).

Teslimatlar:

- Belgelenmiş repository ve solution yapısı.
- Backend, web ve Android için sürüm sabitleme ve lint/typecheck kuralları.
- PostgreSQL, Keycloak ve gerekli local servisleri içeren geliştirme Compose tanımı.
- API health, readiness, structured logging ve correlation kimliği.
- Migration üretme, inceleme ve çalıştırma mekanizması.
- Test projeleri, gerçek PostgreSQL integration test harness'i ve CI.
- Secret örnekleri ile gerçek secret ayrımı.
- Tenant/company kapsamı ve PostgreSQL RLS için atılabilir ama ölçülmüş spike.
- Local backup ve restore smoke prosedürü.

Çıkış kapısı:

- Temiz ortamda belgelenmiş komutlarla kurulum tamamlanır.
- CI build, lint, unit ve DB integration temel testlerini çalıştırır.
- Authenticated örnek istek permission ve company scope ile çalışır.
- Başka company verisine erişim negatif testte reddedilir.
- Migration boş ve örnek verili DB üzerinde doğrulanır.
- Restore edilen DB ile smoke test geçer.
- Yeni altyapı sapmaları ADR ile kayıtlıdır.

### MP-03 — Muhasebe çekirdeği ve cari ilk dikey dilim

Amaç: ERP'nin en kritik doğruluk zincirini küçük bir uçtan uca senaryoda kanıtlamak.

Giriş kapısı güncellemesi (27 Ağustos 2026): Repository sahibi `DEC-MP01-001`–`009` ve `012` kapsamında tenant/company, organizasyon boyutları, takvim yılı, TRY fonksiyonel para, günlük manuel kur, hassasiyet, 120/320 geliştirme şablonu, kesin kayıt düzeltmesi, cari allocation/aging ve granüler permission politikalarını yazılı olarak belirledi. KKTC 27/1977 Vergi Usul Yasası madde 114, normal hesap döneminin takvim yılı olduğunu resmi kaynaktan doğrular. Bu kanıtlarla cari ve muhasebe çekirdeğinin karar-backed dilimleri `in-progress` durumuna geçmiştir. `DEC-MP01-010` banka/reconciliation, isimli mali/güvenlik kabulü, resmi hesap eşlemeleri, vergi/e-Fatura ve production no-go maddeleri kendi kapsamlarını ve MP-03 çıkış kapısını bloklamaya devam eder; faz tamamlanmış sayılmaz. Yaşayan geçiş planı [MP-01 firma politikası temel setindedir](docs/project/plans/2026-08-27-mp01-business-policy-baseline.md).

PartyAccount politika uygulama kanıtı (27 Ağustos 2026): `0030_party_account_balance_side_expand`, aynı Party + Company + currency için explicit receivable/payable hesaplarını ayırdı; aynı yönde duplicate hesabı DB unique index ve writer fail-closed doğrulamasıyla reddetti. Mevcut sınıflandırılmamış satırlar otomatik 120/320 tahminiyle değiştirilmedi; yeni unclassified insert DB check ile engellendi. Boş DB'de 30/0 migration ve 29 migration + mevcut legacy hesaplı DB'de 1/0 upgrade/idempotency ile tüm PostgreSQL tenant/company RLS kontrolleri geçti. Domain 60 check, architecture 19 project ve format kapıları başarılıdır. Explicit opening event→posted journal kanıtı ve authoritative Party source adapter hâlâ tamamlanmadığından MP-03 çıkış kapısı değişmez.

Teknik spike durumu (22 Ağustos 2026): `DEC-MP01-019` sınırında, gerçek posting veya firma politikası üretmeyen dört saf domain dilimi tamamlandı. Decimal journal satırı, tenant/company/source/rule-version bağlamı, effective/recorded tarih ayrımı, immutable doğrulanmış taslak ve tam borç=alacak invariantı [ilk yaşayan görev planında](docs/project/plans/2026-08-21-accounting-kernel-technical-spike.md) kanıtlıdır. ACC-INV-005 için canonical source identity ve in-memory duplicate journal intent kontrolü [ikinci teknik spike planında](docs/project/plans/2026-08-22-accounting-source-uniqueness-spike.md) kanıtlıdır; PostgreSQL unique index ve production concurrency garantisi kapsam dışıdır. PARTY-INV-001/002/003 ve PARTY-INV-005'in aynı para birimli alt kümesi için immutable ödeme/açık-kalem kapasitesi ile çoklu allocation doğrulaması [üçüncü teknik spike planında](docs/project/plans/2026-08-22-party-allocation-invariants-spike.md) yerel doğrulamadan ve GitHub Actions run `32556744288` içindeki altı CI işinden geçmiştir; kapasiteler caller snapshot'ıdır ve authoritative bakiye, eşzamanlılık, posted allocation, unallocation veya FX politikası değildir. ACC-INV-004 ve ACC-PER-001/002/003 için scoped dönem kilidi, ileri kapanış geçişi ve fail-closed standart posting doğrulaması [dördüncü teknik spike planında](docs/project/plans/2026-08-22-period-lock-invariants-spike.md) yerel doğrulamadan ve GitHub Actions run `32558068480` içindeki altı CI işinden geçmiştir; snapshot authoritative dönem kaydı değildir ve reopen yetkisi vermez. Bu çalışmalar MP-03 giriş kapısını geçmiş veya fazı business implementation için başlatmış sayılmaz.

Teknik kanıt güncellemesi (24 Ağustos 2026): `ACC-INV-005` için PostgreSQL kaynak rezervasyonu, tenant/company RLS, append-only runtime ayrıcalıkları, iki paralel bağlantıda tek kazanan unique constraint ve boş/restored veritabanı migration doğrulaması [persistence spike planında](docs/project/plans/2026-08-24-journal-source-reservation-persistence-spike.md) kanıtlandı. Accounting-owned transaction-bound adapter aynı V1 draft fingerprint retry'sinde ilk rezervasyonu döndürüyor, farklı içerikte typed conflict üretiyor ve caller rollback'ine katılıyor; kanıtı [writer spike planındadır](docs/project/plans/2026-08-24-journal-source-reservation-writer-spike.md). Rezervasyon + audit + outbox birlikte commit ve birlikte rollback davranışı [atomiklik spike planında](docs/project/plans/2026-08-24-journal-reservation-audit-outbox-atomicity-spike.md) gerçek PostgreSQL üzerinde geçti. Append-only, non-posted journal header/line snapshot'ı `numeric(20,4)` kayıpsızlık kontrolü, forced RLS, idempotent retry ve reservation + draft + audit + outbox atomikliğiyle [validated draft persistence planında](docs/project/plans/2026-08-24-validated-journal-draft-persistence-spike.md) boş ve mevcut PostgreSQL üzerinde kanıtlandı. Bu snapshot posted mali sonuç değildir; dönem/hesap yetkilendirmeli posting orchestration, API idempotency cevabı, permission ve business-owner onayı tamamlanmadığından MP-03 durumu `proposed` kalır.

Teknik kanıt güncellemesi (25 Ağustos 2026): Accounting Application katmanında company-scope `accounting.journal.post` permission'ı ile aynı draft'a bağlı hesap, boyut, kur ve açık GL/hard-legal dönem kanıtlarını birleştiren fail-closed [posting candidate kapısı](docs/project/plans/2026-08-25-journal-posting-candidate-gate-spike.md) tamamlandı. Aday posted durum üretmez; authoritative period/effective-date lookup, approval, API idempotency ve transaction-bound posted persistence hâlâ tamamlanmadığından MP-03 `proposed` kalır.

Authoritative dönem teknik kanıtı (25 Ağustos 2026): Effective date'i PostgreSQL dönem aralığına bağlayan, sıfır/çoklu eşleşmede fail-closed davranan, canonical transaction advisory lock sonrası dönemi yeniden doğrulayan ve GL/hard-legal state'lerini okuyan [period gate persistence spike](docs/project/plans/2026-08-25-authoritative-period-gate-persistence-spike.md) gerçek mevcut/boş PostgreSQL, RLS ve concurrent-close protokolü üzerinde geçti. Dönem authoring/close/reopen workflow'u, approval ve posted persistence tamamlanmadığından MP-03 `proposed` kalır.

Journal preparation teknik kanıtı (25 Ağustos 2026): Authoritative dönem kapısı, company-scope posting permission ve aynı draft hesap/boyut/kur doğrulamaları; kaynak rezervasyonu, immutable non-posted draft, audit ve `journal-draft-prepared` outbox fact'leriyle [tek caller-owned transaction orchestration'ında](docs/project/plans/2026-08-25-journal-preparation-orchestration-spike.md) birleştirildi. Gerçek PostgreSQL testleri tam commit, tam rollback, yetkisiz aktör ve kapalı dönem için sıfır kısmi persistence kanıtladı. Approval, API idempotency response ve posted journal/GL persistence hâlâ kapsam dışı olduğundan MP-03 `proposed` kalır.

API idempotency teknik kanıtı (25 Ağustos 2026): Tenant/company/actor/command/key kapsamında PostgreSQL unique yarış kontrolü, canonical request hash, completed status/body replay ve farklı payload için `IDEMPOTENCY_KEY_REUSED` davranışı [idempotency persistence spike planında](docs/project/plans/2026-08-25-api-idempotency-persistence-spike.md) mevcut/boş PostgreSQL üzerinde geçti. Runtime yalnız completion kolonlarını güncelleyebilir ve trigger yalnız `in-progress → completed` geçişini kabul eder. Public journal endpoint'i, authoritative master-data loader, approval ve posted journal/GL persistence tamamlanmadığından MP-03 `proposed` kalır.

Authoritative hesap kanıtı (25 Ağustos 2026): Immutable chart version ve account posting snapshot'ları ile journal'ın tüm distinct hesaplarını tenant/company scope içinde yükleyen [account evidence spike](docs/project/plans/2026-08-25-authoritative-account-evidence-spike.md) mevcut/boş PostgreSQL üzerinde geçti. Eksik, pasif, summary ve cross-company hesaplar fail-closed; runtime evidence tablolarında read-only'dir. Boyut/kur authoritative loader, approval ve posted journal/GL persistence tamamlanmadığından MP-03 `proposed` kalır.

Authoritative boyut kanıtı (25 Ağustos 2026): Posting-rule version'a bağlı immutable required-dimension setini tenant/company scope içinde yükleyen [dimension evidence spike](docs/project/plans/2026-08-25-authoritative-dimension-evidence-spike.md) mevcut/boş PostgreSQL üzerinde geçti. Set yokluğu, eksik required dimension ve cross-company erişim fail-closed; runtime evidence tablolarında read-only'dir. Kur/yuvarlama authoritative loader, approval ve posted journal/GL persistence tamamlanmadığından MP-03 `proposed` kalır.

Authoritative kur ve yuvarlama kanıtı (25 Ağustos 2026): Draft içindeki exchange-rate ve rounding-policy snapshot'larını immutable PostgreSQL kanıtıyla tenant/company scope içinde birebir eşleştiren [currency evidence spike](docs/project/plans/2026-08-25-authoritative-currency-evidence-spike.md) mevcut/boş PostgreSQL ve restore akışında geçti. Eksik, değiştirilmiş ve cross-company kanıt fail-closed; runtime evidence tablolarında read-only ve oranlar `numeric(28,12)`'dir. Kur authoring/import, mali politika approval'ı ve posted journal/GL persistence tamamlanmadığından MP-03 `proposed` kalır.

Authoritative preparation composition kanıtı (25 Ağustos 2026): Caller-supplied hesap, boyut ve kur doğrulamalarını preparation request'ten kaldıran; permission'ı veri erişiminden önce denetleyip dönem, hesap, boyut ve kur kanıtlarını aynı PostgreSQL transaction'ında authoritative kaynaklardan yükleyen [composition spike](docs/project/plans/2026-08-25-authoritative-preparation-composition-spike.md) mevcut/boş PostgreSQL ve tam repository doğrulamasında geçti. Kaynak belge→canonical draft application contract'ı, approval ve posted journal/GL persistence tamamlanmadığından public endpoint açılmadı ve MP-03 `proposed` kaldı.

Canonical journal source portu kanıtı (25 Ağustos 2026): Dış command'dan journal draft ve mali snapshot'ları çıkaran, permission-first çalışıp source identity'den aynı transaction içinde server-side canonical draft yükleyen [source port spike](docs/project/plans/2026-08-25-canonical-journal-source-port-spike.md) gerçek PostgreSQL ve tam repository doğrulamasında geçti. Source adapter farklı tenant/company/source identity döndürürse hiçbir fact yazılmadan fail-closed davranır. Gerçek belge adapter'ları, onaylı posting rule eşlemeleri, approval ve posted journal/GL persistence tamamlanmadığından public endpoint açılmadı ve MP-03 `proposed` kaldı.

Canonical source version kanıtı (25 Ağustos 2026): Preparation command'ı beklenen belge sürümüne bağlayan ve server-side adapter'ın authoritative sürümü farklıysa hiçbir fact yazmadan `JOURNAL_SOURCE_VERSION_MISMATCH` üreten [version binding spike](docs/project/plans/2026-08-25-canonical-source-version-binding-spike.md) gerçek PostgreSQL ve tam repository doğrulamasında geçti. Bu bağ idempotency hash ve approval snapshot invalidation için teknik ön koşuldur; gerçek belge adapter'ı, approval ve posted journal/GL persistence tamamlanmadığından MP-03 `proposed` kalır.

Idempotent journal preparation composition kanıtı (25 Ağustos 2026): Canonical source identity ve expected version hash'iyle PostgreSQL idempotency acquire→preparation→completed response akışını tek caller-owned transaction'da birleştiren [composition spike](docs/project/plans/2026-08-25-idempotent-journal-preparation-composition-spike.md) gerçek PostgreSQL ve tam repository doğrulamasında geçti. Replay source adapter'ı yeniden çağırmadan ilk response'u döndürür; changed-version payload conflict ve rollback sonrası temiz retry kanıtlandı. Gerçek source adapter, approval ve posted journal/GL persistence tamamlanmadığından public endpoint açılmadı ve MP-03 `proposed` kaldı.

Approval completion evidence teknik kanıtı (25 Ağustos 2026): Tenant/company ve exact subject version'a bağlı immutable approval instance/workflow version kanıtı; distinct decision, distinct approver, maker-checker ve parametrik quorum invariantlarıyla [domain spike planında](docs/project/plans/2026-08-25-approval-completion-evidence-domain-spike.md) tam repository doğrulamasından geçti. Model quorum veya eligible approver politikasını seçmez. Authoritative approval persistence/loader, gerçek workflow policy ve posted journal/GL persistence tamamlanmadığından MP-03 `proposed` kalır.

Authoritative approval persistence kanıtı (26 Ağustos 2026): Workflow-owned completed instance/decision snapshot'ları exact subject version uniqueness, distinct approver constraint, forced tenant/company RLS ve runtime read-only sınırıyla [persistence spike planında](docs/project/plans/2026-08-26-authoritative-approval-evidence-persistence-spike.md) gerçek mevcut/boş PostgreSQL ve tam repository doğrulamasından geçti. Transaction-bound loader eksik, eski sürümlü ve cross-company kanıtı fail-closed reddediyor. Gerçek workflow policy/write command, preparation/posting composition ve posted journal/GL persistence tamamlanmadığından MP-03 `proposed` kalır.

Approval-gated journal preparation kanıtı (26 Ağustos 2026): Canonical source type/event ID ve expected version'dan server-side türetilen subject için authoritative completed approval'ı reservation/draft/audit/outbox'tan önce aynı transaction'da yükleyen [composition spike](docs/project/plans/2026-08-26-approval-gated-journal-preparation-composition-spike.md) gerçek PostgreSQL ve tam repository doğrulamasında geçti. Missing approval sıfır journal fact ile fail-closed; idempotent replay source ve approval yolunu yeniden çalıştırmıyor. Gerçek source adapter, workflow policy/write command ve posted journal/GL persistence tamamlanmadığından public endpoint açılmadı ve MP-03 `proposed` kaldı.

Posted journal persistence kanıtı (26 Ağustos 2026): Validated draft satırlarını immutable posted header/GL line snapshot'ına server-side kopyalayan; draft, period ve exact source-version approval bağlarını composite FK ile koruyan [persistence spike](docs/project/plans/2026-08-26-posted-journal-persistence-spike.md) gerçek PostgreSQL ve tam repository doğrulamasında geçti. Deferred DB guard line count ve debit/credit toplamlarını header'a cross-foot ediyor; dengesiz owner-tamper commit'i reddedildi. Internal journal ID yasal yevmiye numarası değildir. Üst seviye prepare→post→audit/outbox composition, reversal persistence, resmi numaralama ve gerçek source/workflow command'ları tamamlanmadığından public endpoint açılmadı ve MP-03 `proposed` kaldı.

Journal posting composition kanıtı (26 Ağustos 2026): Approval-gated canonical preparation, immutable posted journal ve birbirinden ayrı posted audit/outbox fact'leri [tek caller-owned transaction composition'ında](docs/project/plans/2026-08-26-journal-posting-composition-spike.md) birleştirildi. Gerçek PostgreSQL testi tam commit'te posted journal/audit/outbox'ın birer kez oluştuğunu, zorlanmış outbox hatasında bütün preparation ve posting fact'lerinin rollback edildiğini doğruladı; tam repository kapısı geçti. Final posted idempotency response composition, reversal persistence, resmi numaralama ve gerçek source/workflow command'ları tamamlanmadığından public endpoint açılmadı ve MP-03 `proposed` kaldı.

Idempotent journal posting composition kanıtı (26 Ağustos 2026): Canonical request hash'li acquire, approval-gated prepare→post→audit/outbox ve HTTP 201 final posted response completion [tek caller-owned transaction'da](docs/project/plans/2026-08-26-idempotent-journal-posting-composition-spike.md) birleştirildi. Gerçek PostgreSQL testi completed replay'in source/approval/posting'i yeniden çalıştırmadan ilk sonucu döndürdüğünü, changed source version'ın aynı anahtarda conflict olduğunu ve zorlanmış outbox hatasında idempotency ile bütün posting fact'lerinin rollback edildiğini kanıtladı. Reversal persistence, resmi numaralama ve gerçek source/workflow command'ları tamamlanmadığından public endpoint açılmadı ve MP-03 `proposed` kaldı.

Posted journal reversal persistence kanıtı (26 Ağustos 2026): Original ve exact-opposite counter journal arasında immutable, company-scoped, tekil bağ [reversal persistence planında](docs/project/plans/2026-08-26-posted-journal-reversal-link-persistence-spike.md) gerçek PostgreSQL üzerinde kanıtlandı. DB guard hesap/source-line/boyut, functional ve transaction currency snapshot'ları ile debit-credit tersliğini doğruluyor; iki connection yarışında tek reversal kazandı, cross-company okuma ve runtime update/delete reddedildi. Reversal date/correction-period, permission/approval, audit/outbox/idempotency command composition ve resmi numaralama hâlâ seçilmediğinden public endpoint açılmadı ve MP-03 `proposed` kaldı.

Party account ve due-schedule persistence kanıtı (26 Ağustos 2026): Minimal tenant-scoped party identity, company/currency scoped party account ve immutable taksit snapshot'ları [persistence spike planında](docs/project/plans/2026-08-26-party-account-due-schedule-persistence-spike.md) gerçek PostgreSQL üzerinde kanıtlandı. Deferred DB guard line count ve original amount toplamını header'a tam cross-foot ediyor; owner-tamper commit'i, cross-company okuma ve runtime update/delete reddedildi. Aynı source/version replay'i tüm taksit içeriği eşleşirse ilk sonucu döndürüyor, tutarı yeniden dağıtan payload conflict oluyor. Payment-term üretimi, remaining projection, allocation/unallocation, FX ve public API henüz tamamlanmadığından MP-03 `proposed` kaldı.

Open-item impact persistence kanıtı (26 Ağustos 2026): Due-schedule line'a bağlı allocation/unallocation/write-off etkileri mutable remaining alanı olmadan [append-only persistence spike planında](docs/project/plans/2026-08-26-open-item-impact-persistence-spike.md) gerçek PostgreSQL üzerinde kanıtlandı. Exact counter DB guard original türü, party account, due line, payment, currency ve amount bağını koruyor; değiştirilmiş retry, owner-tamper counter, cross-company okuma ve runtime update/delete reddedildi. Treasury payment otoritesi, approval, FX, GL composition ve public API tamamlanmadığından backlog 16 ve MP-03 `proposed` kaldı.

Authoritative open-item as-of kanıtı (26 Ağustos 2026): Persisted due-line ve bütün immutable impact history'den effective-date + recorded-cutoff ile remaining üreten [transaction-bound loader](docs/project/plans/2026-08-26-authoritative-open-item-snapshot-loader-spike.md) gerçek PostgreSQL'de kanıtlandı. Late-recorded unallocation geçmiş kesime sızmadı, current kesitte exact counter sonrası bakiye geri açıldı ve cross-company lookup fail-closed kaldı. Public API/permission ile payment/approval/FX/GL composition tamamlanmadığından MP-03 `proposed` kaldı.

Open-item concurrent capacity kanıtı (26 Ağustos 2026): Runtime due-line UPDATE yetkisi genişletilmeden transaction advisory lock ve DB net-capacity guard'ı [concurrency spike planında](docs/project/plans/2026-08-26-open-item-capacity-concurrency-guard-spike.md) gerçek PostgreSQL üzerinde kanıtlandı. 60 GBP due-line'a yarışan 40 + 30 GBP allocation'dan yalnız ilki commit oldu; 40 GBP due-line'a owner-tamper 41 GBP write-off reddedildi. Payment usable capacity ve approval/FX/GL composition hâlâ tamamlanmadığından backlog 16 ve MP-03 `proposed` kaldı.

Payment economic-event persistence kanıtı (26 Ağustos 2026): Same-currency validated payment draft'ı canonical source/purpose uniqueness ve bütün rate snapshot alanlarıyla [Treasury persistence spike planında](docs/project/plans/2026-08-26-payment-economic-event-persistence-spike.md) append-only PostgreSQL'e taşındı. İki connection yarışında tek event oluştu; changed payment identity, cross-company okuma ve runtime update/delete reddedildi. Kayıt approval/posted/settled/reconciled state veya allocation usable capacity kanıtı olmadığından lifecycle/workflow, Party contract ve GL composition tamamlanmadan MP-03 `proposed` kalır.

Authoritative payment load kanıtı (26 Ağustos 2026): Persisted payment'ın source ve identity-rate snapshot'ını aynı transaction/company scope içinde Treasury domain modeline yeniden kuran [loader spike planı](docs/project/plans/2026-08-26-authoritative-payment-economic-event-loader-spike.md) gerçek PostgreSQL'de geçti; cross-company ID fail-closed kaldı. Lifecycle state ve allocation usable-capacity kanıtı hâlâ bulunmadığından MP-03 `proposed` kaldı.

Normalized statement-line persistence kanıtı (26 Ağustos 2026): Canonical external identity, signed amount/currency, booking/value date, raw-object SHA-256 ve parser version [statement persistence spike planında](docs/project/plans/2026-08-26-statement-line-persistence-spike.md) append-only PostgreSQL'e taşındı. Retry ve iki-connection yarışında tek satır oluştu; changed line identity, cross-company okuma ve runtime update/delete reddedildi. Import adapter/profile, dosya güvenliği, reconciliation approval ve GL composition tamamlanmadığından backlog 17 ve MP-03 `proposed` kaldı.

Authoritative statement-line load kanıtı (26 Ağustos 2026): Persisted normalize banka satırını external identity, amount/date ve hash/parser snapshot'ıyla aynı transaction/company scope içinde Treasury domain modeline yeniden kuran [loader spike planı](docs/project/plans/2026-08-26-authoritative-statement-line-loader-spike.md) gerçek PostgreSQL'de geçti; cross-company lookup fail-closed kaldı. Import ve reconciliation approval tamamlanmadığından MP-03 `proposed` kaldı.

Reconciliation proposal persistence kanıtı (26 Ağustos 2026): Approved sonuçtan ayrı immutable proposal/match snapshot'ı [persistence spike planında](docs/project/plans/2026-08-26-reconciliation-proposal-persistence-spike.md) gerçek PostgreSQL'e taşındı. Deferred guard statement scope/direction ve statement/movement kapasitelerini cross-foot etti; owner-tamper 126/125.50 kapasite aşımı, changed retry, cross-company okuma ve runtime update/delete reddedildi. Approval/tolerance/correction ve GL composition tamamlanmadığından backlog 17 ve MP-03 `proposed` kaldı.

Authoritative reconciliation proposal load kanıtı (26 Ağustos 2026): Proposal header, persisted statement ve movement-capacity snapshot'larını aynı transaction/company scope içinde Treasury domain modeline yeniden kuran [loader spike planı](docs/project/plans/2026-08-26-authoritative-reconciliation-proposal-loader-spike.md) gerçek PostgreSQL'de geçti; cross-company lookup fail-closed kaldı. Loader approval/reconciled state üretmediğinden MP-03 `proposed` kaldı.

Report projection generation manifest kanıtı (26 Ağustos 2026): Rapor scope/as-of/cutoff/version/currency/dimension kesimi ile source watermark ve SHA-256 lineage'ı [projection generation manifest planında](docs/project/plans/2026-08-26-report-projection-generation-manifest-spike.md) append-only PostgreSQL'e taşındı. Idempotent replay, changed-lineage conflict, deferred dimension cross-foot, forced RLS ve runtime update/delete reddi gerçek PostgreSQL'de geçti. Projection writer/job, rapor tanımları, account mapping, aging policy ve public API tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Authoritative report projection generation load kanıtı (26 Ağustos 2026): Persisted report slice, dimension ve source lineage manifestini aynı transaction/company scope içinde Reporting domain modeline yeniden kuran [loader spike planı](docs/project/plans/2026-08-26-authoritative-report-projection-generation-loader-spike.md) gerçek PostgreSQL'de geçti; cross-company lookup fail-closed kaldı. Loader rapor rakamı veya source projection üretmediğinden projection job, rapor tanımları, account mapping, aging policy ve public API tamamlanana kadar backlog 18 ve MP-03 `proposed` kalır.

Party statement projection persistence kanıtı (26 Ağustos 2026): Doğrulanmış statement header ve normalized event/running exposure satırlarını generation manifestine bağlayan [persistence spike planı](docs/project/plans/2026-08-26-party-statement-projection-persistence-spike.md) gerçek PostgreSQL'de geçti. Deferred guard line count, running ve closing exposure değerlerini exact cross-foot etti; changed replay, eksik satırlı owner-tamper, cross-company okuma ve runtime update/delete reddedildi. Source-to-sign/opening acquisition, Parties query contract, permission, aging projection, job ve API tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Authoritative party statement projection load kanıtı (26 Ağustos 2026): Persisted statement header, generation manifesti ve normalize satırları aynı transaction/company scope içinde Reporting domain modeline yeniden kuran [loader spike planı](docs/project/plans/2026-08-26-authoritative-party-statement-projection-loader-spike.md) gerçek PostgreSQL'de geçti; exact domain round-trip sağlandı ve cross-company lookup fail-closed kaldı. Source query contract, permission, aging projection, job ve API tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Aging policy projection snapshot kanıtı (26 Ağustos 2026): Caller'ın generation için açıkça sunduğu policy ID/version ve tam kapsamlı calendar-day bucket aralıklarını [snapshot spike planında](docs/project/plans/2026-08-26-aging-policy-projection-snapshot-spike.md) append-only PostgreSQL'e bağlayan writer gerçek DB'de geçti. Idempotent replay, changed-version conflict, deferred count/coverage guard, silinmiş bucket owner-tamper, cross-company okuma ve runtime update/delete reddedildi. Tenant default/approval seçilmedi; aging item projection, source query contract, permission, job ve API tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Authoritative aging policy projection load kanıtı (26 Ağustos 2026): Persisted policy ID/version ve ordinal bucket aralıklarını aynı transaction/company/generation scope içinde domain snapshot'ına yeniden kuran [loader spike planı](docs/project/plans/2026-08-26-authoritative-aging-policy-projection-loader-spike.md) gerçek PostgreSQL'de geçti; exact round-trip ve cross-company fail-closed davranış doğrulandı. Aging item projection, source query contract, permission, job ve API tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Party aging projection persistence kanıtı (26 Ağustos 2026): Doğrulanmış aging header ve immutable open-item snapshot'larını aynı generation/policy snapshot'ına bağlayan [persistence spike planı](docs/project/plans/2026-08-26-party-aging-projection-persistence-spike.md) gerçek PostgreSQL'de geçti. Deferred item-count/remaining cross-foot, changed-item conflict, silinmiş item owner-tamper, cross-company okuma ve runtime update/delete reddedildi. Bucket summary aynı policy/item'lardan türetilir. Source query contract, permission, job ve API tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Authoritative party aging projection load kanıtı (27 Ağustos 2026): Persisted generation manifesti, policy snapshot, aging header ve item'ları aynı transaction/company scope içinde domain modeline yeniden kuran [loader spike planı](docs/project/plans/2026-08-27-authoritative-party-aging-projection-loader-spike.md) gerçek PostgreSQL'de geçti. Total remaining ve bucket summaries item/policy snapshot'larından yeniden türetildi; exact round-trip ve cross-company fail-closed davranış doğrulandı. Source query contract, permission, job ve API tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Authoritative Party statement-aging cross-foot kanıtı (27 Ağustos 2026): Statement ve aging projection'larını aynı transaction/company scope içinde yükleyip aynı report slice, hesap ve balance-side bağlamında closing exposure ile total remaining değerini exact eşleştiren [composition spike planı](docs/project/plans/2026-08-27-authoritative-party-report-cross-foot-composition-spike.md) gerçek PostgreSQL'de geçti; missing/cross-company birleşim fail-closed kaldı. Source query contract, permission, job, API ve drill-down tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Control-account balance projection kanıtı (27 Ağustos 2026): Aynı generation'a bağlı immutable subledger ve GL balance snapshot'larını [persistence spike planında](docs/project/plans/2026-08-27-control-account-balance-projection-persistence-spike.md) PostgreSQL'e taşıyan writer/loader/reconciliation composition gerçek DB'de geçti. Exact arithmetic constraint, idempotent replay, changed payload conflict, owner-tamper, cross-company lookup ve runtime update/delete reddedildi; aynı kesimde sıfır fark mutabakatı yeniden kuruldu. Source query/account mapping, permission, job, API ve drill-down tamamlanmadığından backlog 18 ve MP-03 `proposed` kaldı.

Permission-first Party report query gate kanıtı (27 Ağustos 2026): Required permission kodunu versioned report tanımından parametrik alan ve authoritative statement-aging composition'dan önce company scope/permission kontrolü yapan [query gate spike planı](docs/project/plans/2026-08-27-permission-first-party-report-query-gate-spike.md) gerçek PostgreSQL'de geçti. İzinli fixture yükleme yaptı; izinsiz istek var olmayan resource ID'lerinde bile lookup öncesi typed denial üretti. `DEC-MP01-012` açık olduğundan production permission code, endpoint ve audit composition seçilmedi; MP-03 `proposed` kaldı.

Audited Party report query composition kanıtı (27 Ağustos 2026): Trusted audit context'i execution scope ile doğrulayıp allowed/denied sorgu sonucunu ortak transaction-bound append-only writer'a aktaran [audit composition spike planı](docs/project/plans/2026-08-27-audited-party-report-query-composition-spike.md) gerçek PostgreSQL'de geçti. Denied audit resource ID sızdırmadı; zorlanmış audit hatasında sonuç fail-closed kaldı ve uygulama rolüne audit SELECT verilmedi. Production permission code, endpoint ve source query/job tamamlanmadığından MP-03 `proposed` kaldı.

Atomic Party report projection publication kanıtı (27 Ağustos 2026): Bütün component slice'larını, statement-aging ve subledger-GL cross-foot'larını write öncesi doğrulayıp manifest→policy→statement→aging→ledger snapshot sırasını tek caller-owned transaction'da birleştiren [publisher spike planı](docs/project/plans/2026-08-27-atomic-party-report-projection-publication-spike.md) gerçek PostgreSQL'de geçti. Tam replay yeni fact üretmedi; farklı generation'lı invalid set zero-write reddedildi. Source query/job, production permission code, endpoint ve drill-down tamamlanmadığından MP-03 `proposed` kaldı.

Party report source contract kanıtı (27 Ağustos 2026): Reporting'in Party tablolarını doğrudan okumadan explicit scope/as-of/cutoff, opening, balance side, control account, currency, watermark/checksum ve immutable open-item/impact fact'lerini alacağı bağımsız [contract spike planı](docs/project/plans/2026-08-27-party-report-source-contract-spike.md) 59 contract/domain check ve 18-project architecture kapısından geçti. Restriction kanıtı yoksa `Unavailable` korunuyor; non-UTC ve cut sonrası impact reddediliyor. Parties adapter, projection builder/job, production permission code, endpoint ve drill-down tamamlanmadığından MP-03 `proposed` kaldı.

Party report projection builder kanıtı (27 Ağustos 2026): Contract başlangıç olayının source type/effective/recorded kanıtlarıyla tamamlandı; bağımsız Reporting application builder source batch'i normalize statement ve aging modellerine dönüştürdü. Exact closing/remaining cross-foot ve unavailable restriction fail-closed davranışı [builder spike planında](docs/project/plans/2026-08-27-party-report-projection-builder-spike.md) 60 check ve 19-project architecture kapısıyla geçti. Parties şemasında balance side/opening kanıtı bulunmadığından adapter bunları tahmin etmedi; adapter/job/API eksikleri nedeniyle MP-03 `proposed` kaldı.

Party source lineage checksum kanıtı (27 Ağustos 2026): Source batch checksum'ı caller girdisi olmaktan çıkarılıp scope, bitemporal kesimler, opening, watermark ve canonical open-item/impact payload'ından length-framed SHA-256 olarak üretildi. Equivalent replay, changed-lineage ve defensive-copy kontrolleri izinli architecture hostunda geçti; standalone unit hostu Windows Application Control tarafından `0x800711C7` ile engellendi. Bu ortam engeli finansal davranış testini zayıflatmak için bypass edilmedi; MP-03 durumu değişmedi.

Party projection pair uyumluluk kanıtı (27 Ağustos 2026): Statement ve aging builder'larının farklı hard-coded report code üretmesi atomic publisher'ın same-slice invariantıyla çeliştiği için kaldırıldı. Pair builder iki projection'ı caller'ın versioned report code'u ve aynı generation kesiminde üretip construction-time exact cross-foot uygular. Solution build 0 warning/error geçti; Windows Application Control yeniden üretilen test hostlarını `0x800711C7` ile engellediğinden runtime tekrar kanıtı ortam engeli olarak açık tutuldu ve MP-03 ilerletilmedi.

Party report projection job orchestration kanıtı (27 Ağustos 2026): Source, aging policy, control-account evidence ve atomic sink'i provider-independent application portlarıyla birleştiren [job spike planı](docs/project/plans/2026-08-27-party-report-projection-job-orchestration-spike.md) tamamlandı. Geçerli fixture tek publish üretti; wrong-company source ve wrong-control-account evidence sink çağrılmadan typed fail-closed reddedildi. Release build ve 19-project architecture/application contract hostu geçti. Gerçek port adapter'ları, Worker schedule, production permission code ve API eksik olduğundan MP-03 `proposed` kaldı.

PostgreSQL Party projection sink kanıtı (27 Ağustos 2026): Transaction-owning sink adapter trusted execution scope'u transaction-local RLS context'e aktarıp application publication portunu mevcut atomic publisher'a bağladı. [Sink spike planındaki](docs/project/plans/2026-08-27-postgres-party-report-projection-sink-spike.md) gerçek PostgreSQL testinde ilk publish oluşturdu, tam replay `Created=false` kaldı ve wrong-company scope bağlantı yazımından önce reddedildi; tam RLS integration harness geçti. Source/policy/control-balance adapter'ları ve Worker schedule eksik olduğundan MP-03 `proposed` kaldı.

Atomic publisher ve sink defense-in-depth güncellemesi (27 Ağustos 2026): Statement/aging hesabıyla ilgisiz fakat kendi içinde eşleşen subledger/GL çiftinin publication'a girebildiği sınır kapatıldı; unrelated balance zero-write reddedildi. Sink source query ile Party/control hesapları, currency, bitemporal cut, report definition ve generation bağını connection öncesi yeniden doğruluyor; değiştirilmiş report code typed context mismatch üretti. Yeni negatifler dahil tam PostgreSQL/RLS harness geçti; MP-03 diğer adapter/job/API eksikleri nedeniyle `proposed` kaldı.

Party statement drill-down anchor kanıtı (27 Ağustos 2026): Persisted statement satırını exact company/projection-generation/statement/event kimlikleriyle source lineage taşıyan domain anchor'a kuran [drill-down spike planı](docs/project/plans/2026-08-27-party-statement-drill-down-anchor-spike.md) gerçek PostgreSQL'de geçti. Exact source event/due-line/running exposure round-trip edildi; wrong-generation ve cross-company lookup görünmez kaldı. Source-module resolver, production permission code, endpoint ve web akışı eksik olduğundan MP-03 `proposed` kaldı.

Authoritative Party opening/due source composition kanıtı (28 Ağustos 2026): Due source effective date ve exact Accounting posting purpose `0032` expand migration'ıyla açık taşındı; legacy satır tahmini backfill olmadan korundu, yeni eksik kimlikli satır reddedildi. Parties adapter'ı repeatable-read/RLS kesitinde PartyAccount, opening ve due snapshot'larını Accounting'in exact active posted-source evidence portuyla modül sınırını bozmadan birleştirdi. Gerçek PostgreSQL zincirinde unposted kaynaklar dışlandı; posted `25 GBP` opening + `75 GBP` due üretildi ve cross-company okuma görünmez kaldı. Impact posting identity ile dispute/block evidence eksik olduğundan allocation sonrası source batch fail-closed kalır; MP-03 `in-progress` durumu değişmez.

Party open-item impact source composition kanıtı (28 Ağustos 2026): `0033` expand migration'ı allocation/unallocation/write-off impact'lerine canonical source type/version/posting-purpose ekledi; pre-0033 legacy satır uydurma backfill olmadan korundu, yeni eksik kimlik reddedildi ve loader legacy içeriği typed fail-closed durdurdu. Accounting exact source lifecycle'ı `NotPosted`, `Active` veya original + reversal kanıtlı `Reversed` olarak aynı bitemporal kesitte üretir. Parties adapter'ı original ve counter impact'i ayrı doğrular; gerçek PostgreSQL zincirinde unposted allocation etkisiz kaldı, posting sonrası `75 GBP` due `65 GBP` oldu, unposted unallocation etkisiz kaldı ve posting sonrası kalan `75 GBP` olarak geri açıldı. Original journal reversed iken counter aktif bırakılan çift-ters senaryo typed conflict ile reddedildi. Boş DB'de `33/0`, mevcut verili DB'de `1/0` migration, tam RLS/invariant paketi ve repository kapıları geçti; dispute/block evidence eksikliği nedeniyle MP-03 `in-progress` kalır.

Party dispute/block source evidence kanıtı (28 Ağustos 2026): `0034+0035` migration'ları ihtilaf ve tahsilat blokajını reason/effective/recorded/actor taşıyan append-only applied/released stream olarak ekledi; runtime role due-line UPDATE verilmeden aynı-kind yarışları owner-held fixed-search-path trigger ile serialize edildi. Permission gate, exact release, chronology, immutable replay ve bitemporal loader tamamlandı. Authoritative Party adapter sıfır olayda kanıtlı `Clear`, aktif akışta `Disputed`/`Blocked`/`DisputedAndBlocked` üretir; kalem toplam bakiyede kalır. Gerçek PostgreSQL'de duplicate-active, cross-company ID, append-only privilege ve late-recorded release negatifleri; mevcut DB'de `2/0`, boş DB'de `35/0` migration geçti. Party source adapter milestone'u tamamlandı; gerçek source→projection job/sink→GL golden cross-foot henüz çalıştırılmadığından MP-03 `in-progress` kalır.

Uygulama sırası:

1. Tenant, company, user scope ve dönem bağlamı.
2. Hesap kartı, posting rule sürümü, journal header ve journal line.
3. Cari taraf, adres/iletişim ve şirket kapsamı.
4. Vade planı ve append-only açık kalem.
5. Ödeme veya tahsilat ekonomik olayı.
6. Allocation ve kontrollü unallocation.
7. Dengeli, kaynak bağlı ve idempotent journal.
8. Cari alt defteri ile GL kontrol hesabı mutabakatı.
9. Cari ekstre, aging ve as-of rapor.
10. Audit, outbox, correlation ve hata kanıtı.
11. Rol ve company kapsamlı web akışı.
12. Contract, integration, invariant, E2E ve restore testleri.

Golden senaryo:

- Bir cari için üç ayrı vadeli açık kalem oluşturulur.
- Kısmi tahsilat iki açık kaleme farklı tutarlarda allocate edilir.
- Ödeme, allocation ve banka mutabakatı ayrı durumlar olarak görülür.
- Aynı komut tekrarlandığında ikinci ticari olay veya journal oluşmaz.
- Allocation kaldırıldığında ödeme ve journal silinmez; kontrollü düzeltme izi kalır.
- Cari alt defter toplamı ile ilgili GL kontrol hesabı aynı company, currency ve as-of kesiminde sıfır fark verir.
- Ekstre ve aging satırından kaynak olay, allocation ve journal'a drill-down yapılır.
- Başka company kullanıcısı hiçbir kayıt, export veya hata detayı göremez.
- Backup'tan restore edilen ortamda aynı mutabakat tekrar üretilebilir.

Çıkış kapısı:

- Golden senaryo otomatik testte ve UAT kontrolünde geçer.
- Borç ve alacak toplamı her posted journal için eşittir.
- Kaynak olay, alt defter, allocation, banka reconciliation ve GL ayrı fakat izlenebilirdir.
- Reversal ve correction akışı yerinde update/delete kullanmaz.
- As-of rapor ve kontrol hesabı mutabakatı sıfır fark verir.
- Yetki, audit, idempotency, concurrency ve restore kanıtı vardır.

### MP-04 — Stok ve satış çekirdeği

Amaç: Miktar, maliyet, cari ve muhasebe etkisini tek satış zincirinde uzlaştırmak.

Teslimatlar:

- Ürün, birim, depo, lot/seri gereksinimi ve stok policy.
- Sipariş, rezervasyon, sevk, fatura ve iade durum makineleri.
- Eşzamanlı rezervasyon ve eksi stok koruması.
- Etkin tarihli stok hareketi ve kontrollü değerleme/repost.
- Satış kaynak olayı, cari açık kalem, gelir/KDV ve maliyet posting'i.
- Web akışı, operasyon raporu ve exception görünürlüğü.

Çıkış kapısı:

- Siparişten iadeye miktar korunumu kanıtlanır.
- Stok alt defteri miktar/değer toplamı GL stok kontrol hesabıyla uzlaşır.
- Backdate ve kapanış cut-off testleri geçer.
- Paralel rezervasyon eldeki kullanılabilir miktarı aşmaz.
- İptal ve iade audit zincirini korur.

### MP-05 — Satın alma, banka, kasa ve çek/senet

Amaç: Borç, ödeme, tahsilat, banka ve kıymetli evrak süreçlerini kontrol altında tamamlamak.

Teslimatlar:

- Talep, teklif, sipariş, mal kabul, fatura ve üçlü eşleştirme.
- Banka/kasa hesapları, statement import, matching ve reconciliation.
- Ödeme/tahsilat ile allocation ayrımı.
- Çek/senet durum olayları, teslim, ciro, tahsil ve iade.
- Maker-checker ve limit bazlı onaylar.
- Cari, banka ve GL kontrol raporları.

Çıkış kapısı:

- Üçlü eşleştirme tolerans ve exception akışlarıyla çalışır.
- Statement satırı tekrar importta çift hareket üretmez.
- Ödeme, allocation ve reconciliation bağımsız ama izlenebilir durumdadır.
- Çek/senet geçmişi append-only ve durum makinesine uygundur.
- Cari/banka alt defterleri GL kontrol hesaplarıyla sıfır fark verir.

### MP-06 — KKTC vergi ve resmi e-Fatura

Amaç: Resmi olarak doğrulanmış, tarih etkili ve sürümlü uyum davranışı üretmek.

Bu faz, yazılı doğrulama gerektiren alanlar nedeniyle başlangıçta blocked durumundadır. Blokaj, çekirdek mimariyi veya mock adaptör geliştirmeyi durdurmaz; gerçek oran, format ya da production gönderimi varsayımla etkinleştirmeyi durdurur.

Giriş kapısı:

- Vergi türü, oran, istisna, yuvarlama ve yürürlük tarihleri doğrulanmıştır.
- UBL-KKTC şema, profil, endpoint, kimlik doğrulama, numara ve imza gereksinimi resmen doğrulanmıştır.
- İptal, red, durum sorgu, retry ve arşiv sorumlulukları yazılıdır.
- Test ve production ortamı ayrımı onaylanmıştır.

Çıkış kapısı:

- Kurallar hard-code değil, tarih etkili ve sürümlüdür.
- Kullanılan kural snapshot'ı belgeden ve journal'dan izlenebilir.
- Şema ve iş kuralı doğrulama testleri vardır.
- Unknown-result ve tekrar gönderim güvenli yönetilir.
- Yetkili uzman kabulü ve resmi sandbox kanıtı kayıtlıdır.

### MP-07 — Üretim kalitesinde web istemcisi

Amaç: Orta ölçekli firmanın günlük operasyonunu sade, rol bazlı ve erişilebilir web deneyiminde toplamak.

Teslimatlar:

- shadcn/ui sadeliğinde ortak design token ve component sistemi.
- Rol bazlı navigasyon; ancak server yetkisinin yerine geçmeyen görünürlük.
- Liste, filtre, detay, belge durumu, onay, audit ve drill-down desenleri.
- Büyük veri için server-side pagination, virtualisation ve güvenli export.
- Hata, boş durum, loading, retry ve concurrency çatışması deneyimi.
- Kritik finansal ve operasyonel akışlar için Playwright E2E.

Çıkış kapısı:

- Klavye ve erişilebilirlik kontrolleri geçer.
- Kritik akışlar desteklenen tarayıcılarda E2E olarak geçer.
- Tokenlar browser storage içinde tutulmaz.
- Başka company verisi URL, ID tahmini, export veya cache üzerinden sızmaz.
- Performans bütçeleri ve kullanıcı kabul kriterleri karşılanır.

### MP-08 — Android pilot istemcisi

Amaç: Masaüstü ERP'yi kopyalamadan, sahada değer üreten dar mobil akışları güvenle sunmak.

İlk önerilen kapsam:

- Güvenli giriş ve company seçimi.
- Cari arama ve özet.
- Stok/depo sorgulama.
- Onay bekleyen işler.
- Tahsilat taslağı veya kanıt yükleme; kritik posting server otoritesinde.
- Bildirimden güvenli deep link.

Çıkış kapısı:

- System browser ve PKCE ile giriş çalışır.
- Token cihazın güvenli saklama alanındadır.
- Offline cache tenant/company ile bölünür ve logout'ta temizlenir.
- Retry ve WorkManager idempotent API ile çalışır.
- Kaybolmuş/root edilmiş cihaz ve ekran görüntüsü riskleri değerlendirilir.
- Desteklenen cihazlarda kritik Compose UI testleri geçer.

### MP-09 — Veri migrasyonu, pilot ve paralel kapanış

Amaç: Eski sistem verisini doğrulanmış kurallarla taşımak ve gerçek kullanıcı sürecini kontrollü kanıtlamak.

Teslimatlar:

- Kaynak envanteri, alan eşleme ve veri sahibi.
- Profiling, temizleme, tekrar kayıt ve kod dönüşüm kuralları.
- Yeniden çalışabilir staging ve import pipeline.
- Açılış bakiyesi, cari, stok, banka ve GL mutabakatı.
- Pilot kullanıcılar, eğitim ve destek kanalı.
- En az bir paralel dönem/kapanış ve imzalı UAT.

Çıkış kapısı:

- Kayıt sayısı, tutar, para birimi ve kontrol hesabı mutabakatları imzalıdır.
- Hatalı kayıtlar sessiz atılmaz; exception listesi ve sahibi vardır.
- Cutover provası süre, rollback ve iletişim planıyla tamamlanır.
- Restore ve eski sisteme dönüş kararı prova edilmiştir.
- Go-live sahibi yazılı onay vermiştir.

### MP-10 — Production, DR ve hypercare

Amaç: Yazılımı güvenli biçimde işletmek, kayıptan geri döndürmek ve ilk dönem risklerini yönetmek.

Giriş kapısı:

- Release kabulü, güvenlik kontrolü ve migration provası tamamdır.
- Şifreli ve bağımsız backup hedefi çalışır.
- RPO/RTO ölçülmüş restore provasıyla karşılanır.
- Monitoring, alert, runbook, on-call ve escalation sahipleri bellidir.
- Production secret, DNS, TLS ve firewall değişiklikleri onaylıdır.

Çıkış kapısı:

- Production smoke ve finansal kontrol raporları geçer.
- Backup başarısı kadar restore başarısı da izlenir.
- Olay, güvenlik, veri düzeltme ve rollback runbook'ları denenmiştir.
- 30/60/90 günlük hata, kullanım, kapanış süresi ve iş faydası ölçülür.
- Hypercare açık işleri sahip ve hedef tarihle normal bakıma devredilir.

## 10. Risk sınıflandırması

Codex görevi başlamadan önce en yüksek geçerli sınıfı seçer:

| Sınıf | Örnek | Minimum davranış |
|---|---|---|
| R0 | Yazım, link, açıklama, davranış değiştirmeyen doküman | Belge doğrulama ve diff inceleme |
| R1 | Lokal UI, düşük etkili refactor, test yardımcı kodu | Hedef test, lint/typecheck, geriye uyum kontrolü |
| R2 | API, normal domain davranışı, yeni tablo veya sorgu | Görev planı değerlendirmesi, DB/contract/authorization testi |
| R3 | Posting, para, stok maliyeti, tenant, auth, migration, backup/restore | Yazılı plan, negatif test, invariant, restore/rollback ve ikinci gözden geçirme |
| R4 | Mevzuat yorumu, production, gerçek ödeme/fatura, veri silme veya dış transfer | Açık kullanıcı/uzman onayı; varsayımla yürütme yok |

Bir görev birden çok sınıfa girerse yüksek olan uygulanır. Doküman değişikliği mali davranışı değiştiren bir karar içeriyorsa R0 sayılmaz.

## 11. Standart çalışma döngüsü

### A. Sınıflandır

- Kullanıcı sonucunu tek cümleyle yaz.
- İlgili fazı, requirement kimliklerini, domainleri ve risk sınıfını belirle.
- Değişiklik değil yalnız inceleme istendiyse dış sistem veya kod mutasyonu yapma.

### B. Hedefli keşif yap

- Zorunlu belgeleri yönlendirme matrisinden seç.
- Repository içinde isim tahmin etmek yerine arama yap.
- Mevcut test, migration, API ve pattern'leri incele.
- Kullanıcının ilgisiz değişikliklerini koru.

### C. Davranışı modelle

- Happy path, exception ve compensation akışını çıkar.
- State transition ve allowed action'ları tanımla.
- Transaction, idempotency ve concurrency sınırını belirle.
- Kaynak olay, alt defter, allocation, reconciliation ve GL etkisini ayır.
- Yetki, scope, audit, rapor ve as-of etkisini yaz.

### D. Planla

- Definition of Ready kontrolünü yap.
- En küçük dikey dilimi ve kapsam dışını belirle.
- Migration, rollback/roll-forward ve restore yolunu yaz.
- Test ve kabul kanıtını uygulamadan önce tanımla.

### E. Uygula

- Domain ve veri bütünlüğünden dış katmanlara ilerle.
- Aynı işte gereken migration, OpenAPI, client, audit, metric ve docs değişikliğini tamamla.
- Ölçülmüş gereksinim olmadan yeni altyapı katmanı ekleme.

### F. Olumsuz yolları kanıtla

- Yetkisiz ve yanlış company erişimi.
- Aynı komutun tekrarı.
- Paralel güncelleme veya rezervasyon.
- Yarım kalan dış entegrasyon.
- Kapalı dönem ve backdate.
- Hatalı veri, limit aşımı ve state transition.

### G. Uçtan uca doğrula

- Hedef testlerden ilgili paket ve E2E testlerine genişle.
- Financial değişiklikte kaynak → alt defter → kontrol hesabı → GL → rapor zincirini uzlaştır.
- Migration veya operasyon değişikliğinde restore/rollback kanıtı üret.
- Diff'i secret, PII, tenant, audit ve compatibility açısından yeniden oku.

### H. Teslim et ve planı güncelle

- Değişen sonucu, çalıştırılan testleri ve açık riskleri açıkça bildir.
- Görev planındaki milestone, karar ve ilerleme günlüğünü güncelle.
- Bir faz kapısı gerçekten ilerlediyse bu master planın durumunu ve kanıtını güncelle.
- Sıradaki en küçük güvenli adımı belirt.

## 12. Definition of Ready

Kodlamaya başlamadan önce ilgili maddeler cevaplanmış olmalıdır:

- [ ] Kullanıcı sonucu ve done when açık.
- [ ] Master fazı ve requirement kimlikleri belli.
- [ ] Davranışın sahibi belge belirlendi.
- [ ] Gerekli firma ve mevzuat kararları biliniyor ya da kapsam dışı.
- [ ] Aktör, permission, tenant/company/branch/warehouse scope tanımlı.
- [ ] Para birimi, tarih, dönem, vergi ve yuvarlama etkisi tanımlı.
- [ ] State transition, idempotency ve concurrency davranışı tanımlı.
- [ ] Kaynak olay, ledger, allocation, reconciliation ve rapor etkisi ayrılmış.
- [ ] Veri migration ve geriye uyumluluk etkisi biliniyor.
- [ ] Audit, PII ve retention etkisi biliniyor.
- [ ] Test, kabul ve operasyon kanıtı tanımlı.
- [ ] Blokaj sahibi ve karar tarihi var.

R0/R1 işlerde ilgisiz maddeler gerekçeyle uygulanamaz olarak işaretlenebilir. R3/R4 işlerde kritik bir madde cevapsızsa çalışma, güvenli keşif ve tasarımın ötesine geçmez.

## 13. Dikey dilim teslim sözleşmesi

Bir özelliğin kapsamına giren katmanlar birlikte ele alınır:

| Katman | Beklenen çıktı |
|---|---|
| Domain | Değişmezler, state transition, hata ve compensation |
| Veri | Şema, constraint, index, tenant scope, migration |
| Uygulama | Transaction, idempotency, concurrency ve use case |
| Güvenlik | Permission, scope, SoD ve negatif erişim |
| Muhasebe | Kaynak bağ, posting rule snapshot, journal ve reversal |
| API | Sürümlü contract, Problem Details, pagination ve compatibility |
| İstemci | Server otoritesine bağlı durum, erişilebilir hata/loading deneyimi |
| Audit | Kim, ne, ne zaman, hangi kapsamda ve hangi correlation ile |
| Rapor | As-of anlamı, filtreler, toplam kontrolü ve drill-down |
| Operasyon | Log/metric/trace, alert ve gerekirse runbook |
| Kalite | Unit, integration, contract, invariant, E2E ve restore kanıtı |
| Doküman | Requirement, OpenAPI, ADR, plan ve kullanıcı/operasyon notu |

Kapsam dışı bir katman varsa sessizce atlanmaz; neden etkilenmediği görev planında veya teslim notunda belirtilir.

## 14. Minimum test kapıları

| Değişiklik | Zorunlu asgari kanıt |
|---|---|
| Saf domain kuralı | Unit, boundary ve property/invariant test |
| DB veya migration | Gerçek PostgreSQL integration, boş/verili DB, constraint ve lock değerlendirmesi |
| API | Contract, validation, Problem Details, authz ve idempotency |
| Finansal posting | Dengeli fiş, duplicate, reversal, closed-period ve source lineage |
| Cari/allocation | Kısmi, fazla/avans, unallocation, çoklu vade ve as-of |
| Stok | Miktar korunumu, concurrency, valuation, backdate ve repost |
| Banka/çek | Duplicate import, state transition, reconciliation ve audit |
| Rapor | Golden veri, cross-foot, as-of, filtre, export ve drill-down |
| Web | Component, accessibility ve kritik Playwright akışı |
| Android | Repository/ViewModel, Compose kritik akış, offline/retry ve auth |
| Yetki/tenant | Her endpoint/query/export için yanlış scope negatif test |
| Güvenlik | İlgili abuse case, secret/PII ve dependency kontrolü |
| Backup/restore | Otomatik backup doğrulaması, gerçek restore ve smoke |
| Release | Migration, compatibility, UAT, security ve rollback provası |

Bir test çalıştırılamadıysa Codex bunu geçti diye göstermez; nedeni, riski ve çalıştırılacak kesin komutu bildirir.

## 15. Blokaj ve varsayım politikası

### Mutlaka durulacak durumlar

- Güncel KKTC oranı, istisna, beyan veya resmi belge kuralı bilinmiyor.
- Gerçek e-Fatura endpoint'i, credential, imza veya tekrar gönderim sözleşmesi belirsiz.
- Production veri silme, irreversible migration veya backup overwrite gündemde.
- Gerçek banka, ödeme, fatura ya da dış alıcıya işlem gönderilecek.
- Company/branch veri sahipliği veya yetki kapsamı belirsiz.
- Kişisel verinin veya backup'ın ülke dışına çıkması söz konusu.
- Geçmiş posted veriyi değiştirecek ürün kararı isteniyor.
- Kullanıcının mevcut değişiklikleriyle çakışan destructive işlem gerekiyor.

### Güvenli kabul edilebilecek varsayımlar

- Geri döndürülebilir local geliştirme ayarı.
- Davranış değiştirmeyen dosya veya sembol adı; mevcut convention varsa.
- Mock veya sandbox adaptör; gerçek gönderimin kapalı olduğu açıkça belirtilmişse.
- Konfigürasyon anahtarının placeholder değeri; secret içermiyorsa.

Varsayım geçici, görünür ve test edilebilir olmalıdır. Mevzuat veya finansal anlamı olan değer varsayım sayılamaz.

## 16. Karar önceliği

İki çözüm arasında seçim yapılırken sıra şöyledir:

1. Finansal ve veri doğruluğu.
2. Yetki, tenant izolasyonu ve kişisel veri güvenliği.
3. Güncel mevzuata ve yazılı resmi karara uyum.
4. Restore edilebilirlik ve geri dönüş.
5. Audit, açıklanabilirlik ve mutabakat.
6. Operasyonel sadelik.
7. Test edilebilirlik.
8. API ve veri geriye uyumluluğu.
9. Ölçülmüş performans.
10. Geliştirme hızı ve estetik tercih.

Kısa vadeli hız, ilk beş önceliği düşüremez.

## 17. Mimari büyümeyi kontrol etme

Başlangıç çözümü modüler monolittir. Şunlar ölçüm ve ADR olmadan eklenmez:

- Yeni mikroservis.
- Message broker.
- Redis veya dağıtık cache.
- Kubernetes.
- Ayrı raporlama veritabanı.
- Tam event sourcing.
- İkinci backend framework'ü.
- İstemcinin doğrudan veritabanı erişimi.

ADR en az problemi, ölçümü, seçenekleri, güvenlik/operasyon maliyetini, migration yolunu ve geri dönüşü içermelidir. Yalnız gelecekte lazım olabilir gerekçesi yeterli değildir.

Modül sınırı bugün de korunur:

- Başka modülün tablosuna doğrudan yazılmaz.
- Application contract veya yayımlanmış event kullanılır.
- İç finansal kesinleştirme aynı PostgreSQL transaction'ında güçlü tutarlıdır.
- Dış yan etki transactional outbox üzerinden yapılır.

## 18. Veri değişikliği, yedek ve restore disiplini

Her veri değişikliği şu sırayı gözetir:

1. Veri sınıfını, hacmi ve tenant kapsamını belirle.
2. Expand → migrate → contract yaklaşımını tasarla.
3. Constraint ve backfill için bozuk veri raporu üret.
4. Lock, süre, disk ve WAL etkisini ölç.
5. Eski API/istemci sürümleriyle geçiş penceresini tanımla.
6. Roll-forward ve compensation yolunu yaz.
7. Gerekiyorsa değişiklik öncesi doğrulanmış backup al.
8. Restore edilmiş ortamda migration ve smoke test çalıştır.
9. Production migration'ını uygulama açılışına bağlama.

Backup dosyasının varlığı başarı değildir. Şifre çözme, WAL zinciri, uygulama secret'ları, dosya ekleri ve DB birlikte restore edilip işlevsel smoke test geçmelidir.

## 19. Rapor ve mutabakat disiplini

Finansal veya stok raporu şu sözleşmeyi taşır:

- Company, currency, dimension ve as-of kesimi açıktır.
- Dahil edilen durumlar ve effective date kuralı tanımlıdır.
- Toplam satırlarının nasıl cross-foot ettiği test edilir.
- Kaynak belgeye, alt deftere, allocation/reconciliation'a ve journal'a drill-down vardır.
- Aynı snapshot veya rule version ile yeniden üretilebilir.
- Export aynı authorization ve scope kontrolünden geçer.
- Rapor, transactional kaydı sessizce düzeltmez.

Bir alt defter ile GL kontrol hesabı fark verirse fark gizlenmez veya otomatik plug kaydı atılmaz. Fark; kaynak, tarih, tenant/company, para birimi ve posting rule sürümüyle exception olarak araştırılır.

## 20. Codex iletişim standardı

Uzun bir görev sırasında Codex kullanıcıyı kısa ve doğrulanabilir güncellemelerle bilgilendirir:

- Başlangıçta hangi faz ve belgelerin seçildiği.
- Önemli bir risk, çelişki veya blokaj bulunduğunda.
- Uygulama tamamlanıp test aşamasına geçildiğinde.
- Sonuçta değişen davranış, test kanıtı, açık risk ve sonraki adım.

Ara güncellemeler iç düşünce dökümü değildir; karar ve ilerleme kanıtıdır. Son teslim yalnız dosya listesi değil, kullanıcının elde ettiği sonucu öne çıkarır.

## 21. Plan güncelleme disiplini

### Her çalışma sonunda görev planında

- Güncel durum.
- Tamamlanan milestone.
- Değişen dosyalar veya davranışlar.
- Çalıştırılan test ve sonuç.
- Yeni risk/karar.
- Sıradaki tek kesin adım.

### Yalnız kapı değiştiğinde master planda

- Faz durumu.
- Kapı kanıtı.
- Yeni veya kaldırılmış program bağımlılığı.
- Varsayılan sıradaki backlog maddesi.
- Plan sürümü ve değişiklik kaydı.

Master plan günlük ayrıntılı loga dönüştürülmez. Görev ayrıntısı task planında, kalıcı mimari karar ADR'de, mevzuat kararı legal belgede tutulur.

## 22. İlk uygulama backlog'u

Aşağıdaki sıra, kod repository'si başlamadığında varsayılan yürütme sırasıdır:

1. MP-01 karar matrisi için ürün, muhasebe, güvenlik ve operasyon sahiplerini ata.
2. Legal açık soruları blocking ve non-blocking olarak sınıflandır.
3. Şirket, şube, dönem, para, stok değerleme ve onay politikalarını kaydet.
4. MP-02 için docs/project/plans altında repository-bootstrap görev planı oluştur.
5. Solution, backend, web ve Android klasör iskeletini foundation belgesine göre kur.
6. Sürüm sabitleme, format, lint, analyzers ve temel CI oluştur.
7. Local PostgreSQL ve Keycloak Compose ortamını kur; port ve secret sınırlarını uygula.
8. Migration harness'i ve gerçek PostgreSQL integration test altyapısını kur.
9. Tenant/company modeli ile RLS ve uygulama filtresi spike'ını yap.
10. Authentication, permission, scope ve audit context iskeletini kur.
11. Health, readiness, structured log, trace ve outbox temelini kur.
12. Local şifreli backup ve gerçek restore smoke'u kanıtla.
13. MP-03 için accounting-kernel görev planını Definition of Ready kontrolüyle oluştur.
14. Account, period, journal ve posting rule snapshot modelini uygula.
15. Party, due schedule ve open item modelini uygula.
16. Payment, allocation/unallocation ve idempotent journal akışını uygula.
17. Bank statement ve reconciliation durumlarını ilk senaryoya bağla.
18. Cari ekstre, aging ve subledger/GL kontrol raporunu uygula.
19. Rol bazlı web ekranı ile kaynak-to-rapor drill-down akışını tamamla.
20. Golden E2E, tenant negatif, concurrency, migration ve restore testlerini geçir; MP-03 kapısını değerlendir.

Bir backlog maddesi, kendinden önceki teknik ön koşulu atlamak için gerekçe değildir. Ancak MP-01'de dış cevap beklenirken MP-02'nin mevzuattan bağımsız ve geri döndürülebilir işleri paralel yürüyebilir.

## 23. Kaçınılacak çalışma biçimleri

- Bütün ERP'yi tek görev veya tek PR olarak ele almak.
- Her görevde bütün şartname ve yaşayan plan dosyalarını ayrım gözetmeden bağlama yüklemek.
- Yalnız ekranı tamamlayıp DB, yetki, audit, rapor veya testi ertelemek.
- Başka ERP'nin tablo veya kod modelini aynen kopyalamak.
- Resmi oran ve formatları kod içine sabitlemek.
- Mock testle PostgreSQL constraint ve transaction davranışını geçti saymak.
- UI görünürlüğünü server authorization yerine kullanmak.
- Posted kaydı update/delete ile düzeltmek.
- Ödemeyi allocation, allocation'ı banka reconciliation ile aynı durum yapmak.
- Migration yapıp restore veya compatibility yolunu tanımlamamak.
- Backup üretip restore provası yapmamak.
- Mikroservis veya yeni altyapıyı ölçümsüz eklemek.
- Testi geçsin diye invariant veya güvenlik kontrolünü zayıflatmak.
- Master plan, görev planı ve ADR arasında aynı kararı farklı biçimde çoğaltmak.
- Fazı, çıkış kapısı kanıtı olmadan completed yapmak.

## 24. Master plan kabul kriterleri

Bu dosya amacına ulaşmış sayılırsa:

- Codex her görevi bir faz, risk ve belge rotasına bağlayabilir.
- Tüm şartnameyi okumadan gerekli minimum bağlamı seçebilir.
- Karmaşık iş için görev planı açma kararını tutarlı verebilir.
- Muhasebe, tenant, mevzuat ve restore blokajlarını erken fark eder.
- İlk çalışan dikey dilimi bağımlılık sırasını bozmadan seçer.
- Done tanımı kod, veri, test, rapor, güvenlik ve operasyonu kapsar.
- Master fazları yalnız kanıtla ilerler.
- Kullanıcı yalnız devam et dediğinde sıradaki güvenli işi belirleyebilir.

## 25. Varsayılan sonraki hareket

Kullanıcı yeni bir özellik belirtmeden başla veya devam et dediğinde Codex:

1. MP-01 içindeki bloklayan firma ve resmi kararları listeler.
2. Cevap beklemeyen MP-02 işleri için Definition of Ready kontrolü yapar.
3. docs/project/plans altında tarihli repository-bootstrap görev planı oluşturur.
4. Önce temiz kurulum, CI, auth/company scope, migration ve restore edilebilir platformu kurar.
5. MP-02 çıkış kapısını kanıtladıktan sonra MP-03 accounting-kernel planına geçer.

Bu sıra, kullanıcının açıkça başka bir öncelik vermesi halinde yeniden planlanabilir; finansal, güvenlik, mevzuat ve veri koruma kapıları atlanamaz.

## 26. Yetki sınırı

Bu master plan üretim ortamında işlem yapma yetkisi değildir. Production deployment, DNS, firewall, TLS, secret, gerçek banka/e-Fatura bağlantısı, kişisel veri transferi ve veri silme gibi dış veya zor geri döndürülen işlemler için görev kapsamında açık yetki ve ilgili kapıların kanıtı gerekir.

## 27. Değişiklik kaydı

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 19 Ağustos 2026 | İlk master plan; belge yönlendirme, MP-00–MP-10 fazları, kapılar, risk sınıfları, çalışma döngüsü ve başlangıç backlog'u eklendi |
