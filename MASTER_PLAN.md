# KKTC ERP — Codex Master Plan ve Çalışma Stratejisi

Bu dosya, şartname paketinin uygulamaya hangi sırayla ve hangi kanıtlarla dönüştürüleceğini yönetir. İş gereksinimlerinin yerine geçmez; doğru belgeyi doğru görevde okutmak, bağımlılık sırasını korumak, büyük işleri küçük dikey dilimlere bölmek ve Codex'in plansız genişlemesini önlemek için ana yürütme rotasıdır.

> Durum: active  
> Master plan sürümü: 1.0  
> Paket sürümü: v1.2  
> Son güncelleme: 21 Ağustos 2026
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
| MP-03 | Muhasebe çekirdeği ve cari ilk dikey dilim | proposed | Kaynak olaydan rapora uzlaşan uçtan uca akış | Golden senaryo sıfır fark |
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

Teknik spike durumu (21 Ağustos 2026): `DEC-MP01-019` sınırında, gerçek posting veya firma politikası üretmeyen ilk saf domain dilimi uygulanmaktadır. Decimal journal satırı, tenant/company/source/rule-version bağlamı, effective/recorded tarih ayrımı, immutable doğrulanmış taslak ve tam borç=alacak invariantı [yaşayan görev planına](docs/project/plans/2026-08-21-accounting-kernel-technical-spike.md) bağlıdır. Bu çalışma MP-03 giriş kapısını geçmiş veya fazı business implementation için başlatmış sayılmaz.

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
