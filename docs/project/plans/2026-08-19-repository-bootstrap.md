# MP-02 Repository ve Geliştirme Platformu Planı

- **Amaç:** Kagu ERP için bağımsız, temiz ortamda kurulabilir, sürümleri sabitlenmiş ve güvenlik sınırları tanımlı monorepo geliştirme tabanı oluşturmak.
- **Master fazı ve kapısı:** MP-02 / temiz kurulum, CI, scoped auth örneği, migration ve restore smoke çıkış kapısı.
- **Risk sınıfı:** R3 — auth/tenant, PostgreSQL/RLS, migration, secret ve backup/restore içerir.
- **Durum:** in-progress
- **Sahip:** Teknik lider, güvenlik/veri sorumlusu ve operasyon sorumlusu; isim atamaları MP-01 içinde bekleniyor.
- **Başlangıç / hedef tarih:** 2026-08-19 / isimli sahip ve kapasite doğrulamasından sonra belirlenecek.
- **İlgili requirement ID'leri:** ARCH-001–011, API-001–010 ve MP-02 çıkış kapısı; IAM/DATA/SEC/OPS/DR requirement aileleri.
- **Etkilenen belgeler/modüller:** Repository, backend, web, Android, PostgreSQL, Keycloak, CI, gözlemlenebilirlik ve local restore.
- **Okunan zorunlu belgeler:** `AGENTS.md`, `MASTER_PLAN.md`, `PLANS.md`, `docs/00-foundation/00-technical-foundation.md`, `docs/00-foundation/03-repository-and-code-structure.md`, `docs/00-foundation/04-data-architecture.md`, `docs/00-foundation/05-api-contracts.md`, `docs/project/01-codex-development-workflow.md`, `docs/quality/01-testing-and-quality-strategy.md`, `docs/quality/02-security-and-threat-model.md`, `docs/operations/01-linux-server-deployment.md`, `docs/operations/02-backup-restore-disaster-recovery.md`, ADR-0001–0005.
- **Definition of Ready sonucu:** conditional-pass. Bağımsız repository kararı `DEC-MP01-018` ile onaylıdır. Local/sentetik ve geri döndürülebilir bootstrap başlayabilir. Uzak backup, dış telemetry, production secret/topology ve gerçek veri; MP-01 kararları tamamlanmadan kapsam dışıdır.

## Master plan ilişkisi

Bu görev master backlog'un 4–12 numaralı maddelerini küçük milestone'lara böler. MP-02 çıkış kapısı; yalnız klasörlerin varlığıyla değil build/lint/test, authenticated scoped örnek, gerçek PostgreSQL migration/RLS testi ve restore smoke kanıtıyla ilerler.

MP-01 paralel açık kalır. Firma topolojisi için atılabilir çok-company model kullanılabilir; bu model production kararı veya MP-03 business policy onayı sayılmaz.

## Bağlam

- `Kagu ERP` klasörü 2026-08-19 tarihinde bağımsız Git repository olarak `main` dalıyla başlatıldı.
- GitHub remote: `https://github.com/KaguLtd/Kagu-ERP.git`; erişilebilir ve başlangıçta boştur.
- Repository'de v1.2 şartname paketi, yaşayan görev/karar kayıtları ve ilk .NET modüler monolit iskeleti vardır.
- Yerel doğrulamada Git 2.54.0, .NET SDK 10.0.204, Node.js 24.15.0 ve pnpm 11.19.0 kullanılabilir bulundu. Java ve Docker bulunamadı.

## Kapsam

### Dahil

- Root repository hijyeni, format ve line-ending kuralları.
- .NET 10 solution, merkezi build/package yönetimi ve modüler monolit başlangıç projeleri.
- pnpm workspace ve React/TypeScript web başlangıcı.
- Android Gradle/Kotlin/Compose başlangıç yapısı; local SDK yoksa doğrulanabilir iskelet ve açık blokaj.
- Local PostgreSQL/Keycloak Compose; yalnız local/private port politikası.
- Migration harness, gerçek PostgreSQL integration test altyapısı ve tenant/company/RLS spike.
- Health/readiness, structured log/correlation, audit context ve outbox temeli.
- Local backup/restore smoke ve runbook.
- CI build/lint/unit/integration/security temel kapıları.

### Dahil değil

- Production deploy, DNS, TLS, firewall veya gerçek secret.
- Uzak backup/storage, dış telemetry ve kişisel veri aktarımı.
- Gerçek banka/e-Fatura/SMTP/push bağlantısı.
- Firma muhasebe politikası veya gerçek posting kuralları.
- MP-03 accounting-kernel iş davranışı.

## Değişmezler ve güvenlik sınırları

- Web/Android doğrudan PostgreSQL'e bağlanmaz.
- Runtime DB rolü owner, superuser veya `BYPASSRLS` olamaz.
- Local Compose production credential içermez; `.env.example` yalnız anahtar isimleri ve açık sentetik placeholder taşır.
- PostgreSQL/Keycloak/API/metrics portları production tanımında public bind edilmez.
- Uygulama startup'ında production migration çalışmaz; ayrı migration komutu/job'u vardır.
- Para/miktar için binary floating point iş modeli kurulmaz.
- Modül başka modülün Infrastructure veya şemasına doğrudan yazamaz.
- Dış yan etki transaction içinde yapılmaz; outbox intent aynı DB transaction'ında yazılır.
- Kaynak kod, test, Compose ve restore komutları production verisi/volume'u silemez.

## Tasarım

- **Repository:** `src/`, `apps/`, `tests/`, `packages/`, `db/`, `deploy/`, `scripts/` kökleri; yalnız ilk milestone'da gerçekten kullanılan klasörler source dosyalarıyla oluşturulur.
- **Backend:** Domain ← Application ← Api/Infrastructure; modül Contracts dar dış yüzeydir.
- **DB:** PostgreSQL 18, module schema, UUIDv7, `numeric`, UTC `timestamptz`, `tenant_id`/`company_id`, runtime/migrator/backup rolleri.
- **API:** `/api/v1`, OpenAPI 3.1, Problem Details, idempotency ve ETag building block'ları.
- **Web:** React/TypeScript strict, generated API client sınırı, TanStack Query, RHF/Zod ve design token altyapısı.
- **Android:** Kotlin/Compose, repository/Room/WorkManager sınırı ve system-browser PKCE hazırlığı.
- **Auth:** Keycloak identity; business permission/scope uygulama tarafında. Web BFF/cookie, Android public PKCE.
- **Observability:** OpenTelemetry correlation; PII/secret loglama yok.
- **Restore:** Local sentetik DB + config/blob restore smoke; önerilen RPO/RTO production taahhüdü değildir.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Repository sınırı ve root hijyeni | Bağımsız root, `main`, `origin`, `.gitignore`, `.gitattributes`, `.editorconfig`, root komut sözleşmesi | completed |
| 2 | Sürüm sabitleme ve backend solution | `global.json`, central props/packages, solution build ve architecture test başlangıcı | completed |
| 3 | Web workspace | pnpm frozen install, TypeScript strict, lint/typecheck/test/build | completed |
| 4 | Android workspace | Gradle wrapper/version catalog, lint/unit build veya kayıtlı SDK blokajı | blocked — scaffold hazır; JDK 17/Android SDK yok |
| 5 | Local Compose | PostgreSQL/Keycloak health ve yalnız local güvenli port doğrulaması | in-progress — tanım hazır; Docker ile runtime health bekliyor |
| 6 | Migration ve DB test harness | Boş/verili gerçek PostgreSQL migration + integration test | in-progress — temiz DB, checksum ve idempotency geçti; sonraki migration ile populated-upgrade kanıtı bekliyor |
| 7 | Tenant/company/RLS spike | App filtresi + RLS + pooled connection negatif testleri | in-progress — DB/RLS ve pool negatifleri geçti; API app filtresi bekliyor |
| 8 | Auth/scope/audit örneği | Authenticated örnek istek, permission/company scope ve audit correlation | pending |
| 9 | Health/telemetry/outbox | Readiness, structured log/trace ve duplicate-safe outbox iskeleti | pending |
| 10 | Local restore smoke | Backup, ayrı hedefe restore, auth/scope ve DB smoke | pending |
| 11 | CI ve temiz kurulum | Belgelenmiş bootstrap/verify ile clean checkout kapısı | in-progress — workflow hazır, ilk remote run bekliyor |

## Test planı

- Unit: building block ve domain sınırları.
- Architecture: module/layer dependency yasakları.
- DB integration: PostgreSQL constraint, transaction, role ve RLS.
- Contract: OpenAPI/Problem Details/auth/idempotency örneği.
- Security: secret scan, yanlış tenant/company ve browser/mobile auth sınırı.
- Migration: boş ve örnek verili DB, ileri uyumluluk, lock/rollback değerlendirmesi.
- Restore: sentetik backup'ın ayrı local hedefe gerçek restore'u ve smoke.
- Web: typecheck, component smoke, erişilebilirlik temeli.
- Android: Gradle lint/unit; unavailable local SDK açık blokaj olarak raporlanır.
- Uygulanmaz: MP-03 golden accounting cycle bu platform görevinde henüz yoktur.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-08-19 | Repository başlangıçta üst Git kökünün altındaydı | Kardeş proje kapsam riski | `DEC-MP01-018` ile bağımsız repo oluşturuldu |
| 2026-08-19 | GitHub remote boş | Geçmiş/branch conflict yok; ilk commit henüz yayımlanmadı | Kullanıcı remote'u sağladı; push ayrıca yapılacak |
| 2026-08-19 | İsimli teknik/güvenlik/ops sahipleri yok | MP-02 çıkış kabulü tamamlanamaz | MP-01 içinde atanacak |
| 2026-08-19 | Yerel Node.js 24.15.0 ve pnpm 11.19.0, hedef 24.19.0 ve 11.22.0'ın gerisinde | Temiz kurulum hedef sürümleri ayrıca indirmeli | Hedefler resmi sürüm kaynaklarıyla sabitlendi; mevcut sürümler uyumluluk aralığında |
| 2026-08-19 | Java, Android SDK ve Docker yerelde bulunamadı | Android ve Compose runtime milestone'ları şu anda doğrulanamaz | Kullanıcı JDK 17, Android Studio/SDK ve Docker Desktop kurulumunu başlattı; tanımlar bu sırada hazırlanıyor |
| 2026-08-19 | Android lint/unit ve Compose UI testleri çalıştırılamadı | Android kaynaklarının derleme/cihaz kanıtı yok | JDK 17 + Android SDK kurulumu gerekli; root verify bunu warning ve açık kapı olarak taşır |
| 2026-08-19 | TypeScript 7.0.2, typescript-eslint 8.67.0 peer aralığının dışında | Lint tip bilgisi güvenilir değil | Resmi 6.0 bakım sürümü olan TypeScript 6.0.3'e sabitlendi; `pnpm peers check` temiz |
| 2026-08-19 | İlk web runtime bağımlılıkları eklendi | Lisans, bakım ve bundle yüzeyi | React, TanStack Query, React Hook Form, React Router ve Zod güncel kararlı tam sürümlere kataloglandı; doğrudan runtime paketlerinin tamamı MIT |
| 2026-08-19 | CI üçüncü taraf action çalıştırır | Tag hareketiyle supply-chain riski | Checkout/setup/Gradle/Gitleaks action referansları resmi major tag'lerin tam commit SHA'larına sabitlendi; Dependabot review PR'ı açar, otomatik merge yok |
| 2026-08-19 | Gitleaks action organizasyon/private repo koşulu ilk remote run'da doğrulanmadı | Secret scan job lisans nedeniyle bloklanabilir | İlk GitHub Actions koşusu kapı kanıtıdır; başarısızsa CLI/container alternatifi ayrı kararla seçilecek |
| 2026-08-19 | Production veri konumu/RPO-RTO kararı açık | Uzak backup ve dış telemetry kapsam dışı | Yalnız local sentetik smoke |
| 2026-08-19 | Migration/runtime aynı DB kimliğiyle çalışırsa RLS ve DDL sınırı zayıflar | Tenant sızıntısı veya schema değiştirme riski | Login olmayan `kagu_erp_schema_owner`, NOINHERIT migrator ve owner/superuser/BYPASSRLS olmayan runtime rolleri ayrıldı |
| 2026-08-19 | Yeni PostgreSQL istemci bağımlılığı gerekir | Lisans, bakım ve supply-chain yüzeyi | Resmi NuGet'teki güncel kararlı Npgsql 10.0.3 merkezi pinlendi; paket PostgreSQL lisanslıdır |

## İlerleme günlüğü

### 2026-08-19

- Bağımsız Git repository `main` dalıyla başlatıldı.
- `origin` remote'u `https://github.com/KaguLtd/Kagu-ERP.git` olarak eklendi.
- Remote erişimi ve boş başlangıç durumu doğrulandı.
- Root hijyeni, line-ending/format kuralları, secret ve build çıktısı ignore kapsamı oluşturuldu.
- .NET 10 için `global.json`, merkezi build/package yönetimi; Node.js 24.19.0 ve pnpm 11.22.0 hedefleri sabitlendi.
- Yerel Git 2.54.0, .NET SDK 10.0.204, Node.js 24.15.0 ve pnpm 11.19.0 doğrulandı; Java ve Docker bulunamadı.
- Yedi kaynak projesi ve bir bağımlılık mimarisi kontrolü içeren `KaguERP.slnx` oluşturuldu.
- API live health endpoint'i `http://127.0.0.1:5099/health/live` üzerinde `{"status":"ok"}` döndürdü.
- `scripts/verify.ps1` başarıyla tamamlandı: restore/build sıfır warning ve error, yedi kaynak projesi için architecture check başarılı, format doğrulaması temiz.
- Root `pnpm verify` artık gerçek web workspace'in lint/typecheck/test/build komutlarını çalıştırır.
- `apps/web` altında React 19, Vite 8 ve TypeScript 6 strict workspace; TanStack Query provider, React Router, Zod ile same-origin API health adapter'ı ve erişilebilir başlangıç kabuğu oluşturuldu.
- Tarayıcı token storage'ı eklenmedi; health isteği same-origin cookie sınırında ve iptal sinyaliyle çalışır. Vite yalnız geliştirmede `/health` yolunu yerel API'ye proxy eder.
- Web `lint`, strict `typecheck`, iki component testi ve production build kapıları geçti. `pnpm peers check` bağımlılık uyumsuzluğu bulmadı.
- Doğrudan web runtime bağımlılıklarının lisansları yerel package manifestlerinden MIT olarak doğrulandı.
- Android için AGP 9.3.1, Gradle 9.5.0, Kotlin 2.4.10, Compose BOM 2026.06.01, compile/target SDK 36 ve minSdk 29 sabitlendi. Gradle dağıtım SHA-256 değeri resmi kaynaktan wrapper'a eklendi.
- Tek Activity Compose kabuğu, güvenli manifest başlangıcı, ortak çalışma bağlamı modeli, iki JUnit testi ve bir Compose semantics testi oluşturuldu.
- Wrapper JAR içinde `GradleWrapperMain.class` ve Android XML kaynaklarının parse edilebilirliği doğrulandı. Wrapper JAR SHA-256: `497c8c2a7e5031f6aa847f88104aa80a93532ec32ee17bdb8d1d2f67a194a9c7`.
- `gradlew.bat --version`, beklendiği gibi `java.exe` bulunamadığı için exit 1 verdi. Android lint/unit/Compose testleri çalıştırılmadı ve başarılı sayılmadı.
- Full `scripts/verify.ps1` tekrar geçti: .NET build sıfır warning/error, architecture check başarılı, web lint/typecheck ve 2 test başarılı, production bundle üretildi; Android eksik toolchain warning'i açık kaldı.
- GitHub Actions için salt-okunur izinli ve iptal edilebilir dört job oluşturuldu: backend, web, Android ve secret scan. Action referansları tam commit SHA'larına pinlendi; credential persistence kapatıldı.
- Dependabot; GitHub Actions, npm/pnpm, Gradle ve NuGet için haftalık ve insan incelemeli güncelleme PR'ları açacak şekilde yapılandırıldı.
- CI henüz remote'a push edilmediği için GitHub-hosted Android ve Gitleaks sonuçları kanıtlanmış sayılmadı.
- Local geliştirme için `postgres:18.4-trixie` tabanlı ayrı ERP/Keycloak veritabanları ve `quay.io/keycloak/keycloak:26.7.0` tanımlandı. ERP DB ve Keycloak yalnız loopback'e yayımlanır; Keycloak DB host'a yayımlanmaz.
- Compose verileri ayrı named volume'larda tutulur; PostgreSQL 18'in sürüme özel `PGDATA` yolu kullanılır. Local servisler yalnız internal Compose ağıyla haberleşir.
- `.env.example` yalnız açık sentetik placeholder taşır. Windows ve POSIX bootstrap betikleri mevcut `.env` dosyasını korur, yoksa rastgele local parolalar üretir; repository'ye production secret eklenmez.
- Compose config, image pull ve PostgreSQL/Keycloak health kontrolleri Docker kurulunca çalıştırılacaktır; tanımın varlığı runtime kapısını geçmiş sayılmaz.
- Ayrı `KaguERP.Migrator` CLI'si eklendi. Connection string yalnız environment üzerinden alınır; migration'lar embedded, sıralı ve SHA-256 checksum ile `platform.schema_migration` tablosunda izlenir. Bilinmeyen/sonradan değiştirilmiş migration fail-closed davranır ve eşzamanlı çalıştırma PostgreSQL advisory lock ile seri hale gelir.
- Local PostgreSQL ilk açılışında login olmayan schema owner, NOINHERIT migrator ve owner/superuser/BYPASSRLS olmayan application rollerini oluşturan bootstrap eklendi. `public` schema CREATE yetkisi kaldırıldı ve sabit `search_path` tanımlandı.
- İlk expand migration'ı `org.tenant` ve `org.company` tablolarını UUID, version, UTC timestamp, aktör ve aktiflik kolonlarıyla kurdu. Runtime rolünde SELECT/INSERT/UPDATE vardır, DELETE yoktur; iki tabloda `ENABLE/FORCE ROW LEVEL SECURITY` ve `WITH CHECK` politikaları bulunur.
- Gerçek ve sıfırdan oluşturulmuş PostgreSQL 18 cluster'ında migration ilk çalışmada 1, ikinci çalışmada 0 migration uyguladı. Runtime rolünün superuser/BYPASSRLS/table-owner olmadığı doğrulandı.
- Aynı gerçek DB koşusunda yetkili tenant/company okuma-yazma başarılı; çapraz tenant okuma-yazma, aynı tenant içindeki yetkisiz company okuma ve DELETE reddedildi. `SET LOCAL` context'inin pooled bağlantıya sızmadığı doğrulandı; sentetik kayıtlar test sonunda kaldırıldı ve geçici cluster durdurulup silindi.
- CI'a PostgreSQL migration/RLS job'u eklendi. Ephemeral parolalar workflow sırasında rastgele üretilir; job henüz remote üzerinde koşmadığı için CI kanıtı bekler.
- PowerShell bootstrap/verify/test betikleri dış komut exit code'larında fail-fast olacak şekilde sertleştirildi; NuGet erişim hatası artık yanlış başarı sonucu üretemez.
- `RestorePackagesWithLockFile` etkinleştirildi ve 10 .NET proje lock dosyası üretildi. `dotnet restore KaguERP.slnx --locked-mode` geçti; bootstrap, verify ve DB test betikleri locked restore kullanır.
- Son full `scripts/verify.ps1` koşusu geçti: locked restore, .NET Release build (0 warning/0 error), 8 source proje architecture kontrolü, format, web lint/typecheck, 2 component testi ve production build başarılı. Android JDK/SDK eksikliği açık warning olarak kaldı.
- Bu yaşayan MP-02 planı oluşturuldu.
- Sıradaki teknik kapılar JDK 17 + Android SDK ile Android build ve Docker ile local Compose runtime doğrulamasıdır. Araçlar kurulurken migration harness için local altyapı sözleşmesi hazırlanabilir.

## Tamamlanma kanıtı

- [x] Bağımsız repository sınırı ve remote.
- [x] Root hijyen ve sürüm sabitleme dosyaları.
- [x] Backend build ve architecture kapısı.
- [x] Web lint/typecheck/component test/build kapısı.
- [x] Android wrapper/version catalog ve statik scaffold kanıtı.
- [ ] Android lint/unit/Compose test kapısı — JDK 17 ve Android SDK blokajı.
- [ ] Local Compose — tanım/bootstrap hazır; Docker runtime health bekliyor.
- [ ] Migration harness — temiz gerçek PostgreSQL, checksum ve idempotency geçti; populated-upgrade senaryosu sonraki migration'ı bekliyor.
- [ ] Tenant/company/RLS negatif testleri — DB ve pool katmanı geçti; API application-filter katmanı bekliyor.
- [ ] Authenticated scoped örnek ve audit.
- [ ] Health/telemetry/outbox temeli.
- [ ] Local restore smoke.
- [ ] CI ve temiz kurulum kanıtı — workflow hazır, ilk remote run bekliyor.
- [ ] Güvenlik/secret/PII incelemesi.
- [ ] MP-02 çıkış kapısı değerlendirmesi.
