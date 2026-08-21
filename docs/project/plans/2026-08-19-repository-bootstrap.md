# MP-02 Repository ve Geliştirme Platformu Planı

- **Amaç:** Kagu ERP için bağımsız, temiz ortamda kurulabilir, sürümleri sabitlenmiş ve güvenlik sınırları tanımlı monorepo geliştirme tabanı oluşturmak.
- **Master fazı ve kapısı:** MP-02 / temiz kurulum, CI, scoped auth örneği, migration ve restore smoke çıkış kapısı.
- **Risk sınıfı:** R3 — auth/tenant, PostgreSQL/RLS, migration, secret ve backup/restore içerir.
- **Durum:** completed
- **Sahip:** Roller `DEC-MP01-019` gereği atanmadı; isim atamaları geliştirme sonunda yeniden değerlendirilecek.
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
- GitHub remote: `https://github.com/KaguLtd/Kagu-ERP.git`; `main` dalı ilk bootstrap commit'i `71c8faf` ile yayımlanmıştır.
- Repository'de v1.2 şartname paketi, yaşayan görev/karar kayıtları ve ilk .NET modüler monolit iskeleti vardır.
- Yerel doğrulamada Git 2.54.0, .NET SDK 10.0.204, Node.js 24.15.0, pnpm 11.19.0, Eclipse Temurin JDK 17.0.20 ve Android Studio 2026.1.3/SDK kullanılabilir bulundu. WSL 2.7.12 ve Docker Desktop 4.87.0 (Engine 29.7.2, Compose 5.4.0) çalışmaktadır.

## Kapsam

### Dahil

- Root repository hijyeni, format ve line-ending kuralları.
- .NET 10 solution, merkezi build/package yönetimi ve modüler monolit başlangıç projeleri.
- pnpm workspace ve React/TypeScript web başlangıcı.
- Android Gradle/Kotlin/Compose başlangıç yapısı, local lint/unit/instrumentation derleme ve managed-device Compose testi.
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
| 4 | Android workspace | Gradle wrapper/version catalog, lint/unit/instrumentation derleme ve managed-device Compose testi | completed — JDK 17, Android SDK, lint, 2 JVM testi, test APK'sı ve API 29 emülatörde 1 Compose semantics testi geçti |
| 5 | Local Compose | PostgreSQL/Keycloak health ve yalnız local güvenli port doğrulaması | completed — üç container healthy; host portları yalnız loopback, Keycloak DB host'a kapalı |
| 6 | Migration ve DB test harness | Boş/verili gerçek PostgreSQL migration + integration test | completed — `0002` mevcut `0001` verili şemaya ileri uygulandı; checksum, ikinci koşu idempotency ve gerçek DB integration geçti |
| 7 | Tenant/company/RLS spike | App filtresi + RLS + pooled connection negatif testleri | completed — deny-by-default API scope guard, DB/RLS ve pool negatifleri geçti |
| 8 | Auth/scope/audit örneği | Authenticated örnek istek, permission/company scope ve audit correlation | completed — JWT→ERP DB aktif üyelik/şirket/permission ve correlation zincirli append-only authorization audit geçti |
| 9 | Health/telemetry/outbox | Readiness, structured log/trace ve duplicate-safe outbox iskeleti | completed — DB-backed readiness, JSON route-template telemetry ve transaction/duplicate/scope testli outbox temeli geçti |
| 10 | Local restore smoke | Backup, ayrı hedefe restore, auth/scope ve DB smoke | completed — ayrı rastgele DB'ye pg_dump/pg_restore, migration/RLS/IAM/audit/outbox ve Keycloak auth geçti; cleanup sıfır artıkla doğrulandı |
| 11 | CI ve temiz kurulum | Belgelenmiş bootstrap/verify ile clean checkout kapısı | completed — clean bootstrap ile run `32360372748` içindeki altı job'ın tamamı geçti |

## Test planı

- Unit: building block ve domain sınırları.
- Architecture: module/layer dependency yasakları.
- DB integration: PostgreSQL constraint, transaction, role ve RLS.
- Contract: OpenAPI/Problem Details/auth/idempotency örneği.
- Security: secret scan, yanlış tenant/company ve browser/mobile auth sınırı.
- Migration: boş ve örnek verili DB, ileri uyumluluk, lock/rollback değerlendirmesi.
- Restore: sentetik backup'ın ayrı local hedefe gerçek restore'u ve smoke.
- Web: typecheck, component smoke, erişilebilirlik temeli.
- Android: Gradle lint, JVM unit, instrumentation APK derleme; API 29 x86_64 managed-device üzerinde Compose semantics testi.
- Uygulanmaz: MP-03 golden accounting cycle bu platform görevinde henüz yoktur.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-08-19 | Repository başlangıçta üst Git kökünün altındaydı | Kardeş proje kapsam riski | `DEC-MP01-018` ile bağımsız repo oluşturuldu |
| 2026-08-19 | GitHub remote başlangıçta boştu | Geçmiş/branch conflict olmadan `main` başlatılabildi | `71c8faf` ilk bootstrap commit'i olarak pushlandı; CI run `32229247173` oluşturuldu |
| 2026-08-21 | İsimli teknik/güvenlik/ops sahipleri yok | Production ve uzman kabulü yapılamaz | Kullanıcı `DEC-MP01-019` ile atamayı geliştirme sonuna erteledi; teknik MP-02 kapanışı kabul edildi |
| 2026-08-19 | Yerel Node.js 24.15.0 ve pnpm 11.19.0, hedef 24.19.0 ve 11.22.0'ın gerisinde | Temiz kurulum hedef sürümleri ayrıca indirmeli | Hedefler resmi sürüm kaynaklarıyla sabitlendi; mevcut sürümler uyumluluk aralığında |
| 2026-08-20 | Android Studio 2026.1.3 kuruldu; ilk wizard API 37.1 sistem imajını bulamadı | IDE mevcut olsa da SDK/cihaz testi başlangıçta eksikti | Varsayılan SDK keşfedildi; platform 37.0/build-tools 36.0.0 doğrulandı, API 29 x86_64 Gradle managed device ile gerçek Compose testi geçti |
| 2026-08-20 | Compose `internal` ağı Windows host port yayınını işlevsiz bıraktı | Container'lar healthy olsa da ERP DB ve Keycloak host'tan erişilemiyordu | Ağ bridge olarak tanımlandı; yalnız ERP DB `127.0.0.1:55432` ve Keycloak `127.0.0.1:58080` yayınlanır, Keycloak DB host'a kapalı kalır |
| 2026-08-19 | TypeScript 7.0.2, typescript-eslint 8.67.0 peer aralığının dışında | Lint tip bilgisi güvenilir değil | Resmi 6.0 bakım sürümü olan TypeScript 6.0.3'e sabitlendi; `pnpm peers check` temiz |
| 2026-08-19 | İlk web runtime bağımlılıkları eklendi | Lisans, bakım ve bundle yüzeyi | React, TanStack Query, React Hook Form, React Router ve Zod güncel kararlı tam sürümlere kataloglandı; doğrudan runtime paketlerinin tamamı MIT |
| 2026-08-19 | CI üçüncü taraf action çalıştırır | Tag hareketiyle supply-chain riski | Checkout/setup/Gradle/Gitleaks action referansları resmi major tag'lerin tam commit SHA'larına sabitlendi; Dependabot review PR'ı açar, otomatik merge yok |
| 2026-08-19 | Gitleaks action organizasyon/private repo koşulu ilk remote run'da doğrulanmadı | Secret scan job lisans nedeniyle bloklanabilir | İlk remote run'da secret scan geçti; action bu private repository'de çalışıyor |
| 2026-08-19 | İlk remote CI backend, DB ve Android işlerinde başarısız oldu | Clean-checkout/CI kapısı kapanamaz | Linux path normalizasyonu, bridge Compose ağı ve Android SDK command-line tools tam yolu uygulandı; yerel clean bootstrap geçti, yeni remote run bekliyor |
| 2026-08-19 | CI tarafından üretilen ephemeral local parolalar job logundaki environment bloğunda maskelenmedi | Private logda kısa ömürlü sentetik credential görünürlüğü | Değerler yalnız sonlandırılmış runner/local DB içindi; yeni workflow her değeri `$GITHUB_ENV` yazımından önce `add-mask` ile maskeler, remote kanıt bekliyor |
| 2026-08-19 | Production veri konumu/RPO-RTO kararı açık | Uzak backup ve dış telemetry kapsam dışı | Yalnız local sentetik smoke |
| 2026-08-19 | Migration/runtime aynı DB kimliğiyle çalışırsa RLS ve DDL sınırı zayıflar | Tenant sızıntısı veya schema değiştirme riski | Login olmayan `kagu_erp_schema_owner`, NOINHERIT migrator ve owner/superuser/BYPASSRLS olmayan runtime rolleri ayrıldı |
| 2026-08-19 | Yeni PostgreSQL istemci bağımlılığı gerekir | Lisans, bakım ve supply-chain yüzeyi | Resmi NuGet'teki güncel kararlı Npgsql 10.0.3 merkezi pinlendi; paket PostgreSQL lisanslıdır |
| 2026-08-20 | JWT bearer doğrulaması için yeni ASP.NET paketi gerekir | Token doğrulama ve supply-chain yüzeyi | Resmi Microsoft `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 merkezi pinlendi; MIT lisans, lockfile ve issuer/audience negatif smoke ile sınırlandı |
| 2026-08-20 | Direct password grant modern web/mobil production akışı değildir | Yanlışlıkla production realm'e taşınırsa kimlik güvenliği zayıflar | Client yalnız local import dosyasında sentetik smoke kullanıcıya açıktır; production Authority/config zorunlu ve web/mobil akışları BFF/PKCE kalır |
| 2026-08-20 | MP-02 bootstrap resolver bir `(issuer, subject)` kimliğini tek tenant'a bağlar | Gelecekte aynı IdP kimliğiyle çoklu tenant üyeliği gerekirse mevcut unique constraint yetersiz kalır | İlk dikey dilimde privilege birleşmesini önleyen fail-closed sınır olarak tutuldu; tenant seçimi sözleşmesi netleşmeden gevşetilmeyecek ve sonraki değişiklik expand migration gerektirecek |
| 2026-08-20 | Local API ve worker outbox tablosunda aynı runtime DB rolünü kullanır | API ele geçirilirse scope içindeki outbox satırlarında worker yetkilerine ulaşabilir | MP-02 local iskelette RLS ve ayrı application contract ile sınırlı; production secret/topoloji kararıyla ayrı worker login rolü oluşturulmadan canlı kabul verilmeyecek |

## İlerleme günlüğü

### 2026-08-19

- Bağımsız Git repository `main` dalıyla başlatıldı.
- `origin` remote'u `https://github.com/KaguLtd/Kagu-ERP.git` olarak eklendi.
- Remote erişimi ve boş başlangıç durumu doğrulandı; `main` dalı `71c8faf` ile `origin` üzerine ilk kez pushlandı.
- Yerel `scripts/verify.ps1` kapısı .NET build/architecture ve web lint/typecheck/test/build için geçti; Android JDK/SDK eksikliği açık kapı olarak kaldı.
- İlk GitHub Actions `ci` çalışması `32229247173` tamamlandı: web ve secret scan geçti; backend mimari kontrolü Linux'ta Windows ayraçlı proje yollarını normalize edemedi, DB işi `127.0.0.1:55432` bağlantısını kuramadı ve Android işinde `sdkmanager` bulunamadı.
- CI'nin rastgele ürettiği local/ephemeral parolaların log environment bloğunda maskelenmediği kaydedildi. Bunlar sonlandırılmış runner ve sentetik DB dışında geçerli değildir; workflow düzeltilmeden yeni DB CI koşusu çalıştırılmamalıdır.
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
- Android için AGP 9.3.1, Gradle 9.5.0, AGP 9 built-in Kotlin ile uyumlu Kotlin Compose compiler plugin 2.2.10, Compose BOM 2026.06.01, compile SDK 37.0, target SDK 36 ve minSdk 29 sabitlendi. Gradle dağıtım SHA-256 değeri resmi kaynaktan wrapper'a eklendi.
- Tek Activity Compose kabuğu, güvenli manifest başlangıcı, ortak çalışma bağlamı modeli, iki JUnit testi ve bir Compose semantics testi oluşturuldu.
- Wrapper JAR içinde `GradleWrapperMain.class` ve Android XML kaynaklarının parse edilebilirliği doğrulandı. Wrapper JAR SHA-256: `497c8c2a7e5031f6aa847f88104aa80a93532ec32ee17bdb8d1d2f67a194a9c7`.
- `gradlew.bat --version`, beklendiği gibi `java.exe` bulunamadığı için exit 1 verdi. Android lint/unit/Compose testleri çalıştırılmadı ve başarılı sayılmadı.
- Full `scripts/verify.ps1` tekrar geçti: .NET build sıfır warning/error, architecture check başarılı, web lint/typecheck ve 2 test başarılı, production bundle üretildi; Android eksik toolchain warning'i açık kaldı.
- GitHub Actions için salt-okunur izinli ve iptal edilebilir dört job oluşturuldu: backend, web, Android ve secret scan. Action referansları tam commit SHA'larına pinlendi; credential persistence kapatıldı.
- Dependabot; GitHub Actions, npm/pnpm, Gradle ve NuGet için haftalık ve insan incelemeli güncelleme PR'ları açacak şekilde yapılandırıldı.
- GitHub-hosted Gitleaks işi ilk remote koşuda geçti; Android işi `sdkmanager` bulunamadığı için lint/unit aşamasına ulaşamadı.
- Local geliştirme için `postgres:18.4-trixie` tabanlı ayrı ERP/Keycloak veritabanları ve `quay.io/keycloak/keycloak:26.7.0` tanımlandı. ERP DB ve Keycloak yalnız loopback'e yayımlanır; Keycloak DB host'a yayımlanmaz.
- Compose verileri ayrı named volume'larda tutulur; PostgreSQL 18'in sürüme özel `PGDATA` yolu kullanılır. Keycloak DB host'a yayımlanmaz; host erişimi gereken local servisler yalnız `127.0.0.1` adresine bind edilir.
- `.env.example` yalnız açık sentetik placeholder taşır. Windows ve POSIX bootstrap betikleri mevcut `.env` dosyasını korur, yoksa rastgele local parolalar üretir; repository'ye production secret eklenmez.
- Compose config, image pull ve PostgreSQL/Keycloak health kontrolleri Docker kurulunca çalıştırılacaktır; tanımın varlığı runtime kapısını geçmiş sayılmaz.
- Ayrı `KaguERP.Migrator` CLI'si eklendi. Connection string yalnız environment üzerinden alınır; migration'lar embedded, sıralı ve SHA-256 checksum ile `platform.schema_migration` tablosunda izlenir. Bilinmeyen/sonradan değiştirilmiş migration fail-closed davranır ve eşzamanlı çalıştırma PostgreSQL advisory lock ile seri hale gelir.
- Local PostgreSQL ilk açılışında login olmayan schema owner, NOINHERIT migrator ve owner/superuser/BYPASSRLS olmayan application rollerini oluşturan bootstrap eklendi. `public` schema CREATE yetkisi kaldırıldı ve sabit `search_path` tanımlandı.
- İlk expand migration'ı `org.tenant` ve `org.company` tablolarını UUID, version, UTC timestamp, aktör ve aktiflik kolonlarıyla kurdu. Runtime rolünde SELECT/INSERT/UPDATE vardır, DELETE yoktur; iki tabloda `ENABLE/FORCE ROW LEVEL SECURITY` ve `WITH CHECK` politikaları bulunur.
- Gerçek ve sıfırdan oluşturulmuş PostgreSQL 18 cluster'ında migration ilk çalışmada 1, ikinci çalışmada 0 migration uyguladı. Runtime rolünün superuser/BYPASSRLS/table-owner olmadığı doğrulandı.
- Aynı gerçek DB koşusunda yetkili tenant/company okuma-yazma başarılı; çapraz tenant okuma-yazma, aynı tenant içindeki yetkisiz company okuma ve DELETE reddedildi. `SET LOCAL` context'inin pooled bağlantıya sızmadığı doğrulandı; sentetik kayıtlar test sonunda kaldırıldı ve geçici cluster durdurulup silindi.
- CI'a PostgreSQL migration/RLS job'u eklendi. İlk remote koşu container health sonrasında `127.0.0.1:55432` bağlantısını kuramadı; ayrıca ephemeral parolaların logda maskelenmesi gerekir.
- PowerShell bootstrap/verify/test betikleri dış komut exit code'larında fail-fast olacak şekilde sertleştirildi; NuGet erişim hatası artık yanlış başarı sonucu üretemez.
- `RestorePackagesWithLockFile` etkinleştirildi ve 10 .NET proje lock dosyası üretildi. `dotnet restore KaguERP.slnx --locked-mode` geçti; bootstrap, verify ve DB test betikleri locked restore kullanır.
- Son full `scripts/verify.ps1` koşusu geçti: locked restore, .NET Release build (0 warning/0 error), 8 source proje architecture kontrolü, format, web lint/typecheck, 2 component testi ve production build başarılı. Android JDK/SDK eksikliği açık warning olarak kaldı.
- Bu yaşayan MP-02 planı oluşturuldu.
- Sıradaki teknik kapılar JDK 17 + Android SDK ile Android build ve Docker ile local Compose runtime doğrulamasıdır. Araçlar kurulurken migration harness için local altyapı sözleşmesi hazırlanabilir.

### 2026-08-20

- Eclipse Temurin JDK 17.0.20, WSL 2.7.12 ve Docker Desktop 4.87.0 doğrulandı. Docker Engine 29.7.2 ve Compose 5.4.0 `desktop-linux` context'i üzerinde çalışmaktadır.
- `gradlew.bat --version`, sabitlenmiş Gradle 9.5.0 dağıtımını checksum doğrulamasıyla indirip JDK 17 üzerinde başarıyla çalıştı. Android SDK eksik olduğu için lint/unit/Compose testleri hâlâ başarılı sayılmayan açık kapıdır.
- PowerShell bootstrap ve verify betikleri per-user Docker Desktop kurulumunu PATH güncellenmemiş olsa da standart kurulum konumundan bulacak şekilde sertleştirildi.
- Compose ağı bridge olarak düzeltildi. `erp-db`, `keycloak-db` ve `keycloak` container'ları healthy oldu; yalnız ERP DB `127.0.0.1:55432` ve Keycloak `127.0.0.1:58080` host'a yayımlandı. Keycloak DB host'a yayımlanmadı.
- Keycloak master realm OIDC discovery endpoint'i HTTP 200 verdi. Gerçek Compose PostgreSQL üzerinde migration iki kez 0 yeni migration ile idempotent tamamlandı ve tenant/company RLS, çapraz-scope ve connection-pool negatif testleri geçti.
- Windows Uygulama Denetimi tarafından generated apphost `.exe` dosyalarının engellenmesine karşı doğrulama betikleri derlenmiş assembly'leri `dotnet <assembly>.dll` yoluyla çalıştırır; güvenlik politikası devre dışı bırakılmadı.
- Full `scripts/verify.ps1` geçti: locked restore, .NET Release build (0 warning/0 error), 8 source proje architecture kontrolü, format, web lint/typecheck, 2 component testi, production build, migration idempotency ve gerçek PostgreSQL RLS kontrolleri başarılıdır. Yalnız Android SDK kapısı warning olarak açıktır.
- `/api/v1` için merkezi application-scope middleware eklendi. Anonim istek, istemciden gelen `X-Tenant-Id`/`X-Company-Id`, ERP üyeliği çözülemeyen kimlik, çapraz tenant ve yetkisiz company senaryoları deny-by-default davranır; Problem Details yanıtlarında stabil güvenli kod bulunur.
- `ExecutionScope` yalnız trusted application resolver tarafından sağlanır. Gerçek IAM üyelik/permission resolver'ı milestone 8'e kadar eklenmediğinden varsayılan resolver tüm business API isteklerini fail-closed reddeder; token claim/header tek başına business scope sayılmaz.
- API application-scope contract kontrolleri root PowerShell/POSIX verify akışına bağlandı. Windows Uygulama Denetimi uyumu için .NET test/migration assembly'leri generated apphost yerine `dotnet <assembly>.dll` ile çalıştırılır.
- Gerçek API process smoke testinde `/health/live` HTTP 200; anonim `/api/v1/companies` isteği HTTP 401 ve `AUTHENTICATION_REQUIRED` güvenli hata kodu döndürdü.
- Milestone 7 tamamlandı; milestone 8 authenticated ERP membership/permission/company scope ve audit correlation dilimi başlatıldı.
- Merkezi correlation middleware eklendi. Tek canonical UUID biçimindeki istemci değeri korunur; değer yoksa UUIDv7 üretilir; boş, bozuk veya çoklu değer `400 INVALID_CORRELATION_ID` ile endpoint'ten önce reddedilir.
- Correlation ID response header, güvenli Problem Details, Activity tag ve immutable audit request context içinde aynı değer olarak taşınır. Audit context ayrıca trace, trusted tenant/actor/company scope ve varsa opaque session ID içerir; token/cookie veya business payload taşımaz.
- API contract testleri generated/preserved/invalid correlation, Problem Details korelasyonu ve önceki scope negatiflerini birlikte doğrular. Full `scripts/verify.ps1` yeniden geçti: .NET build/format/mimari/API contract, web lint/typecheck/2 test/build, migration idempotency ve gerçek PostgreSQL RLS kontrolleri başarılıdır.
- Gerçek API smoke testinde health response server correlation ID döndürdü; istemci correlation değeri anonim `401 AUTHENTICATION_REQUIRED` Problem Details ve response header'da aynen korundu; bozuk değer `400 INVALID_CORRELATION_ID` aldı.
- Milestone 8'in sıradaki dilimi, OIDC `iss`/`aud`/signature/expiry doğrulamasından geçen Keycloak subject'ini ERP DB'deki aktif üyelik, permission ve company scope ile çözmek; authorization kararını append-only audit persistence'a bağlamaktır.
- Microsoft `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11 merkezi olarak pinlendi. Paket resmi Microsoft/ASP.NET bileşenidir ve MIT lisanslıdır; JWT signature, issuer, audience, lifetime, expiry ve signed-token kontrolleri açık, inbound claim remapping/token persistence/hata ayrıntısı kapalıdır.
- API production configuration'da Authority/Audience eksikse startup fail-closed olur. Metadata HTTPS varsayılan zorunludur; HTTP yalnız `appsettings.Development.json` içindeki loopback Keycloak authority için açıkça izinlidir.
- Local Compose startup importu `kagu-local-test` realm, bearer-only `kagu-erp-api` audience ve local-only smoke client/user oluşturur. Smoke parolası yalnız ignored `.env` içinde rastgele üretilir; realm JSON'da environment placeholder bulunur ve production akışına direct grant taşınmaz.
- Gerçek Keycloak token smoke matrisi geçti: doğru issuer + `kagu-erp-api` audience tokenı authentication'ı geçip deny-by-default ERP resolver'da `403 APPLICATION_SCOPE_REQUIRED`; aynı kullanıcıya ait fakat ERP audience içermeyen token `401 AUTHENTICATION_REQUIRED` aldı. Response token veya framework hata ayrıntısı içermedi.
- `scripts/test-auth.ps1` discovery issuer'ını ve iki token senaryosunu tekrarlanabilir çalıştırır; token/parola yazmaz, ayrı smoke portu kullanır ve başlattığı API sürecini `finally` içinde kapatır. Root verify bu testi çalışan Keycloak olduğunda otomatik çağırır.
- Windows Uygulama Denetimi yeni ayrı API contract DLL'ini engellediğinde politika kapatılmadı. Aynı kontroller mevcut ve izinli architecture quality harness'ına taşındı; birleştirilmiş harness ile full verify geçti.
- Son full `scripts/verify.ps1`: locked restore, Release build (0 warning/0 error), architecture + API scope/correlation contract, format, web lint/typecheck/2 test/build, migration idempotency, gerçek PostgreSQL RLS ve gerçek Keycloak issuer/audience smoke başarılı. Yalnız Android SDK kapısı warning olarak açıktır.
- `0002_identity_membership_and_permissions` expand migration'ı RLS korumalı `iam.user_profile` ve şirket bazlı, zaman aralıklı `iam.user_company_permission` tablolarını ekledi. Runtime rolü yalnız identity context'iyle SELECT yapabilir; owner/superuser/BYPASSRLS ayrımı korunur. Geri dönüş, tablo düşürme yerine uygulama deploy'unu geri alma ve migration öncesi backup'tan kontrollü restore/compensation yoludur.
- Gerçek PostgreSQL testinde doğru `iss`/`sub` tenant, aktör, iki şirket ve şirket bazlı permission setine çözüldü; yanlış/çoklu claim, bilinmeyen subject, süresi dolmuş permission, şirketler arası permission birleşmesi ve pooled identity-context sızıntısı reddedildi.
- `/api/v1/me/scopes` yalnız `profile.read` izni olan şirketleri döndüren ilk authenticated permission örneği olarak eklendi. Keycloak smoke, doğru token için ERP DB fixture üzerinden HTTP 200; yanlış audience için 401 doğruladı ve fixture'ı sonunda temizledi.
- Windows Uygulama Denetimi yeni Infrastructure test assembly'sini engellediğinde politika kapatılmadı; runtime resolver izinli mevcut Bootstrap assembly'sine alındı ve aynı gerçek DB davranışı integration harness'ında doğrulandı.
- Full `scripts/verify.ps1` tekrar geçti: locked restore, Release build (0 warning/0 error), architecture/API contract, format, web lint/typecheck/2 test/build, populated migration idempotency, gerçek PostgreSQL IAM/RLS ve gerçek Keycloak→ERP permission smoke başarılıdır. Yalnız Android SDK kapısı warning olarak açıktır.
- `0003_append_only_authorization_audit` migration'ı correlation/trace, trusted tenant/actor/company scope, eylem, hedef, sonuç ve güvenli reason code taşıyan `platform.audit_event` tablosunu ekledi. Runtime rolü yalnız RLS kapsamlı INSERT sahibidir; SELECT/UPDATE/DELETE yetkileri yoktur.
- `GET /api/v1/me/scopes` izin/verme ve permission ret kararını audit yazımıyla bağlar; audit yazılamazsa başarılı response üretilmez. Gerçek DB testleri tek kayıt, kapsam doğruluğu, non-append privilege reddi ve başka şirket kapsamıyla audit insert reddini kanıtlar.
- Audit dilimi sonrası full `scripts/verify.ps1` geçti: locked restore, Release build (0 warning/0 error), architecture/API contract, format, web lint/typecheck/2 test/build, migration idempotency, gerçek PostgreSQL IAM/RLS/audit ve gerçek Keycloak→ERP permission smoke başarılıdır. Android SDK warning'i açık kapı olarak korunur.
- `/health/live` proses canlılığıyla sınırlı tutuldu; `/health/ready` üç saniyelik bounded PostgreSQL probe ile hazır/503 ayrımını yapar. Gerçek erişilebilir ve erişilemez PostgreSQL testleri ile API process readiness smoke geçti.
- API ve Worker JSON console log üretir. Request middleware; ham URL, query, header, token veya payload yerine route template, method, status, süre ve correlation ID yazar; aynı boyutlarla .NET `Meter` counter/histogram ve mevcut `Activity` route tag'i üretir. Dış exporter/veri aktarımı MP-01 kararı olmadan eklenmedi.
- `0004_transactional_outbox` migration'ı RLS korumalı event/aggregate sequence, schema version, UTC occurred time, JSON payload, hash, retry/lease ve durum alanlarını ekledi. Writer çağıranın mevcut Npgsql transaction'ına katılır; kendi başına commit açmaz.
- Gerçek DB testlerinde event-id tekrarı tek satır kaldı, aynı ID ile farklı payload ve aynı aggregate sequence reddedildi, çapraz-company scope reddedildi; business company kaydıyla outbox insert'i aynı rollback'te birlikte kayboldu. İleri migration geri dönüşü tablo düşürme değil roll-forward veya migration öncesi restore'dur.
- Windows Uygulama Denetimi yeniden derlenen ayrı Integration harness DLL'ini engellediğinde politika kapatılmadı; DB/auth test modları mevcut izinli Architecture quality harness'ında birleştirildi ve aynı gerçek PostgreSQL kapsamı korundu.
- `scripts/test-restore.ps1` kaynak local ERP DB'sini custom-format `pg_dump` ile yalnız doğrulanmış rastgele `kagu_erp_restore_*` hedefine restore etti. Restore DB'de migration 0/0 idempotency, runtime rol/RLS/IAM/audit/outbox entegrasyonu, readiness ve gerçek Keycloak→ERP auth scope smoke geçti.
- İlk cleanup denemesinde process environment sırası Compose `.env` interpolasyonunu gölgeledi; kaynak veya restore verisi silinmedi. Cleanup sırası düzeltildi, yalnız regex ile doğrulanmış geçici DB/dump kaldırıldı ve son kontrolde restore DB sayısı 0, dump sayısı 0 bulundu.
- POSIX restore betiği aynı izole DB ve DB-scope kontrollerini trap cleanup ile uygular. Local `pg_dump` provası production pgBackRest/WAL/PITR, blob/Keycloak backup veya RPO/RTO taahhüdü sayılmaz.
- Restore kapısı root `scripts/verify.ps1` akışına bağlandı ve full verify geçti: locked restore, Release build 0 warning/error, architecture/safe telemetry, format, web lint/typecheck/2 test/build, kaynak DB migration/RLS/IAM/audit/outbox, Keycloak auth ve ikinci izole restore hedefinde aynı smoke başarılıdır. Yalnız Android SDK kapısı warning olarak açıktır.
- GitHub Actions run `32229247173` job logları salt-okunur incelendi. Backend hatası `.csproj` içindeki Windows ayraçlı `ProjectReference` değerlerinin Linux'ta normalize edilmemesiydi; architecture harness her iki ayıracı platform yoluna çevirir. DB container'ı healthy olmasına rağmen `internal` Compose ağı host portunu erişilemez bırakmıştı; daha önce yerelde doğrulanan bridge düzeltmesi CI hatasının da kök nedenini kapatır.
- CI ephemeral parolaları, `$GITHUB_ENV` dosyasına yazılmadan önce GitHub `add-mask` komutuyla maskelenir. Keycloak smoke parolası da Compose interpolasyonu için aynı rastgele ve maskeli akışa eklendi. Android işi runner'daki SDK command-line tools için `${ANDROID_HOME}/cmdline-tools/latest/bin/sdkmanager` tam yolunu kullanır.
- CI database işi migration/RLS testinden sonra POSIX izole restore smoke'u da çalıştırır. Ayrı `Clean bootstrap` işi temiz checkout'ta locked NuGet restore ve frozen pnpm install yapar ve tracked dosyaların değişmediğini doğrular.
- Yerelde 196 source dosyasından `.git`, `.env`, `node_modules`, `bin` ve `obj` içermeyen bağımsız geçici kopya üretildi. `scripts/bootstrap.ps1 -SkipServices`; yeni rastgele `.env`, locked NuGet restore ve frozen pnpm install ile geçti; servis/volume açılmadı ve geçici kopya doğrulama sonunda silindi.
- CI portability düzeltmelerinden sonra full `scripts/verify.ps1` tekrar geçti: Release build 0 warning/error, architecture/API contract/safe telemetry, format, web lint/typecheck/2 test/build, gerçek PostgreSQL migration/RLS/IAM/audit/outbox, gerçek Keycloak auth ve izole restore hedefi başarılıdır. Yerel Android SDK eksikliği açık warning'dir; remote CI kanıtı değişiklikler commit/push edilmeden üretilemez.
- Repository'ye girecek 196 dosyada private-key/cloud-token/bearer ve literal credential örüntü taraması yapıldı; bulunan password/token referansları yalnız `CHANGEME_LOCAL_ONLY_*`, environment aktarımı veya loglanmayan smoke değişkenleridir. `.env` tracked değildir; auth smoke başarı mesajı token/parola taşımaz. NuGet doğrudan ve transit paket taraması ile `pnpm audit --audit-level high` bilinen zafiyet bulmadı. İlk remote Gitleaks işi geçti; mevcut değişiklikler için yeni remote secret scan hâlâ CI kanıtının parçasıdır.
- Android Studio 2026.1.3 varsayılan SDK'sı `%LOCALAPPDATA%\Android\Sdk` altında keşfedildi. Root PowerShell/POSIX verify betikleri SDK ortam değişkeni yokken standart konumu bulur, JDK 17 ile platform 37.0/build-tools 36.0.0 varlığını fail-fast doğrular ve `lintDebug testDebugUnitTest assembleDebugAndroidTest` çalıştırır.
- AGP 9 built-in Kotlin geçişi nedeniyle eski `org.jetbrains.kotlin.android` plugin'i kaldırıldı; AGP 9.3.1'in metadata bağımlılığıyla uyumlu Kotlin Compose plugin 2.2.10 kullanıldı. Lifecycle 2.11.0 gereksinimi nedeniyle compile SDK 37.0'a çıkarıldı; target SDK 36 ve minSdk 29 değiştirilmedi. Deprecated Compose test rule importu `junit4.v2` API'sine taşındı.
- `pixel2Api29` x86_64 Gradle managed device tanımlandı. İlk koşu API 29 AOSP sistem imajını lisanslı SDK akışıyla kurdu; 1 Compose semantics instrumentation testi emülatörde geçti. Root verify'ın instrumentation APK derlemesinden ayrı bu koşu `:app:pixel2Api29DebugAndroidTest` göreviyle tekrarlanabilir.
- Android düzeltmelerinden sonraki full `scripts/verify.ps1` geçti: .NET Release build 0 warning/error, architecture/API contract/safe telemetry, format, web lint/typecheck/2 test/build, gerçek PostgreSQL migration/RLS/IAM/audit/outbox, gerçek Keycloak auth, izole restore ve Android lint/2 JVM testi/instrumentation APK derlemesi başarılıdır. Yerel MP-02 Android blokajı kapanmıştır; yalnız değişikliklerin commit/push edilmesinden sonraki temiz remote CI kanıtı açıktır.
- Remote CI run `32359676674` içinde clean bootstrap, backend/format, web, Android ve secret scan geçti; database migration/RLS de geçti. Yalnız POSIX restore, executable biti taşımayan `scripts/test-db.sh` dosyasını doğrudan çalıştırdığı için exit 126 aldı. İç çağrı `bash ./scripts/test-db.sh` olarak taşınabilir hale getirildi; Git Bash üzerinde shell parse ve gerçek izole restore/migration/RLS/outbox zinciri geçti. Düzeltme için yeni remote koşu beklenmektedir.
- Düzeltme commit'i `2f4d4ee` için GitHub Actions run `32360372748` başarıyla tamamlandı. Clean bootstrap, backend build/architecture/format, web lint/typecheck/test/build, Android SDK/lint/unit/instrumentation build, Gitleaks secret scan ve PostgreSQL migration/RLS/izole restore job'larının 6/6'sı geçti; ephemeral database cleanup tamamlandı.

## Tamamlanma kanıtı

- [x] Bağımsız repository sınırı ve remote.
- [x] Root hijyen ve sürüm sabitleme dosyaları.
- [x] Backend build ve architecture kapısı.
- [x] Web lint/typecheck/component test/build kapısı.
- [x] Android wrapper/version catalog ve statik scaffold kanıtı.
- [x] Android lint/unit/Compose test kapısı — lint, 2 JVM testi, instrumentation APK derlemesi ve API 29 managed-emulator üzerinde 1 Compose semantics testi geçti.
- [x] Local Compose — üç servis healthy, loopback port sınırı ve OIDC discovery doğrulandı.
- [x] Migration harness — temiz ve örnek verili gerçek PostgreSQL, checksum, ileri migration ve idempotency geçti.
- [x] Tenant/company/RLS negatif testleri — deny-by-default API application guard, DB/RLS ve pooled connection katmanları geçti.
- [x] Authenticated scoped örnek ve audit — Keycloak→ERP permission/company scope ve append-only correlation audit geçti.
- [x] Health/telemetry/outbox temeli — DB readiness, güvenli JSON route telemetry ve transactional duplicate-safe outbox gerçek DB testleri geçti.
- [x] Local restore smoke — ayrı hedefte migration, DB scope/outbox ve gerçek Keycloak auth geçti; kaynak DB/volume değişmedi ve geçici artık kalmadı.
- [x] CI ve temiz kurulum kanıtı — bağımsız yerel temiz bootstrap ve GitHub Actions run `32360372748` içindeki 6/6 job geçti.
- [x] Güvenlik/secret/PII incelemesi — tracked kaynak örüntü taraması ve güncel NuGet/pnpm vulnerability sorguları temiz; yeni remote Gitleaks koşusu CI maddesinde bekliyor.
- [x] MP-02 çıkış kapısı değerlendirmesi — tüm teknik yerel/remote kanıtlar mevcut; kullanıcı `DEC-MP01-019` ile isimli atamaları erteleyip teknik kapanışı kabul etti. Production/uzman kabulü kapsam dışıdır.
