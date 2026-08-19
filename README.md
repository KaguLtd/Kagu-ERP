# KKTC ERP — Codex Teknik Spesifikasyon Paketi

Bu klasör, KKTC'de orta ölçekli bir işletme için geliştirilecek web ve Android erişimli ERP'nin kaynak-koddan önce gelen teknik sözleşmesidir. Belgeler; mimariyi, modül sınırlarını, veri bütünlüğünü, güvenliği, testleri, Linux dağıtımını, yedekleme/geri dönüşü ve KKTC uyum noktalarını Codex'in küçük ve doğrulanabilir işler halinde uygulayabileceği şekilde tanımlar.

> Durum: Tasarım tabanı v1.2 — 19 Ağustos 2026. v1.2, araştırmayla güçlendirilmiş v1.1 şartnamesini korur ve Codex'in belge seçimi, faz sırası, kalite kapıları ve varsayılan sonraki işini yöneten kök [MASTER_PLAN.md](MASTER_PLAN.md) dosyasını ekler. Canlı mevzuat hükümleri, KDV cetvelleri ve resmi entegrasyon onayları uygulama öncesinde KKTC'de yetkili muhasip/murakıp ve ilgili kamu kurumlarıyla doğrulanmalıdır.

## Uygulama durumu ve yerel doğrulama

MP-02 repository bootstrap başlamıştır. Bağımsız repository `main` dalını ve `https://github.com/KaguLtd/Kagu-ERP.git` remote'unu kullanır. İlk backend solution, katman referans kontrolleri, strict React/TypeScript web workspace, Android/Compose proje iskeleti ve local PostgreSQL/Keycloak Compose tanımı mevcuttur. Android build ve Compose çalışma zamanı kurulacak JDK/Android SDK/Docker ile doğrulanacaktır; auth/RLS ve restore milestone'ları henüz tamamlanmamıştır.

Hedef toolchain:

- .NET 10 LTS; `global.json` aynı major içindeki güncel feature band'ine roll-forward eder.
- Node.js 24 LTS; `.node-version` güncel güvenlik yamalı sürümü gösterir.
- pnpm sürümü root `package.json` içinde sabitlenir.
- Java/Android SDK ve Docker, ilgili MP-02 milestone'unda ayrıca doğrulanır.

Tüm mevcut yerel kapıları çalıştırmak için:

```powershell
./scripts/verify.ps1
```

Linux/macOS/CI için:

```bash
./scripts/verify.sh
```

İlk yerel kurulum ve bağımlılık geri yükleme için:

```powershell
./scripts/bootstrap.ps1
```

Linux/macOS/CI eşdeğeri:

```bash
./scripts/bootstrap.sh
```

Bootstrap, mevcut `.env` değerlerini değiştirmez; yeni zorunlu anahtar eksikse onu ekler. Dosya yoksa Git tarafından yok sayılan `.env` içine kriptografik rastgele ve yalnız yerelde kullanılacak parolalar üretir. Docker varsa PostgreSQL 18 ve Keycloak'ı da başlatıp health kapılarını bekler. Local servisler:

- ERP PostgreSQL: `127.0.0.1:55432` — yalnız API/backend erişimi içindir; web ve Android doğrudan bağlanmaz.
- Keycloak: `http://localhost:58080` — `start-dev` yalnız yerel geliştirme içindir.
- Keycloak PostgreSQL: yalnız private Compose ağı; host portu yoktur.

`.env.example` içindeki değerler açıkça güvensiz placeholder'lardır. Local Compose veya üretilen `.env` production dağıtım tanımı ya da production secret kaynağı değildir.

PostgreSQL migration ve tenant/company RLS integration kontrollerini ayrıca çalıştırmak için:

```powershell
./scripts/test-db.ps1
```

```bash
./scripts/test-db.sh
```

Bu komut migration CLI'sini iki kez çalıştırarak checksum/idempotency davranışını, ardından gerçek PostgreSQL üzerinde runtime rol ayrımı ve RLS negatif senaryolarını doğrular. `KaguERP.Migrator` uygulama başlangıcından ayrıdır; connection string komut satırı argümanı olarak kabul edilmez ve yalnız environment üzerinden verilir. Runtime rolü schema/table owner, superuser veya `BYPASSRLS` değildir.

`pnpm verify`; web lint, strict typecheck, component testleri ve production build kapılarını çalıştırır. Vite geliştirme sunucusu `/health` isteklerini yerel API'nin `http://127.0.0.1:5099` adresine yönlendirir. Root doğrulama scripti JDK 17 ve Android SDK mevcutsa Android lint/unit testlerini de çalıştırır; eksikse bunu geçen test olarak değil açık MP-02 kapısı olarak raporlar.

## Değişmez ana kararlar

- Başlangıç mimarisi **modüler monolit** olacak; modüller aynı PostgreSQL veritabanını kullansa da şema, kod ve API sınırları korunacak.
- Backend **.NET 10 LTS / ASP.NET Core**, veritabanı **PostgreSQL 18**, web **React + TypeScript + Vite + shadcn/ui**, Android **Kotlin + Jetpack Compose** olacak.
- Web ve Android istemcileri veritabanına doğrudan bağlanmayacak; yalnız sürümlenmiş API üzerinden çalışacak.
- Kesinleşmiş finansal, stok, cari ve çek/senet hareketleri değiştirilmeyecek veya silinmeyecek; düzeltme karşı kayıtla yapılacak.
- Kaynak ticari olay, alt defter, ödeme–açık kalem tahsisi, banka mutabakatı ve büyük defter birbirinden ayrı fakat kaynak kimliğiyle uzlaşan kayıt katmanları olacak.
- Etkin/yasal tarih ile sisteme kayıt ve posting zamanı ayrı tutulacak; geçmiş tarihli stok/muhasebe etkisi kontrollü yeniden hesaplama ve mutabakat ister.
- KDV, belge biçimi ve benzeri mevzuat kuralları kod içine sabit yazılmayacak; tarih etkili ve sürümlenmiş olacak.
- Üretim ilk aşamada tek Linux sunucuda Docker Compose ile başlayacak. PostgreSQL, Keycloak ve iç servis portları internete açılmayacak.
- Veritabanı için sürekli WAL arşivleme/PITR; dosyalar ve yapılandırma için ayrı şifreli yedek; düzenli geri dönüş provası zorunlu olacak.

## Okuma sırası

1. [AGENTS.md](AGENTS.md) — Codex'in her görevde uyması gereken kısa ve bağlayıcı kurallar.
2. [MASTER_PLAN.md](MASTER_PLAN.md) — program fazı, belge rotası, giriş/çıkış kapısı ve sıradaki güvenli iş.
3. [Belge indeksi](docs/README.md) — görevin türüne göre yalnız gerekli foundation, modül ve çapraz belgeleri seçmek için.
4. [Teknik temel](docs/00-foundation/00-technical-foundation.md), [ürün kapsamı](docs/00-foundation/01-product-scope-and-principles.md) ve gerektiğinde [sözlük](docs/00-foundation/02-domain-glossary.md).
5. Davranışın sahibi modül ile ilgili istemci, kalite, güvenlik, hukuk veya operasyon belgesi.
6. Karmaşık işler için master fazına bağlı [PLANS.md](PLANS.md) formatında yaşayan görev planı.

## Belge grupları

- `docs/00-foundation/`: Mimari, ürün ilkeleri, veri, API ve ortak süreçler.
- `docs/modules/`: Her iş alanı için ayrı domain/modül sözleşmesi.
- `docs/clients/`: Web, Android ve UI/UX tasarım sistemi.
- `docs/quality/`: Test, güvenlik, performans, migrasyon ve yayın kabulü.
- `docs/operations/`: Linux kurulumu, yedek/restore, gözlem ve bakım.
- `docs/project/`: Codex çalışma biçimi, yol haritası ve bitti tanımı.
- `docs/legal/`: KKTC mevzuat matrisi ve resmi kurum onay kapıları.
- `docs/decisions/`: Kabul edilmiş mimari karar kayıtları (ADR).
- `docs/templates/`: Yeni modül, görev, ADR ve test senaryosu şablonları.
- `docs/references/`: Resmi ve birincil kaynaklar.

## Belge otoritesi

Çelişki halinde öncelik sırası:

1. Güncel yasa/tüzük ve yazılı resmi kurum cevabı.
2. `AGENTS.md` içindeki güvenlik ve bütünlük kuralları.
3. Kabul edilmiş ADR'ler.
4. Temel mimari ve veri/API standartları.
5. Modül belgeleri.
6. `MASTER_PLAN.md` içindeki program sırası ve kalite kapıları.
7. Görev planı ve issue açıklaması.

Bir çelişki bulunduğunda Codex sessizce seçim yapmaz; [resmi onaylar ve açık sorular](docs/legal/02-official-approvals-and-open-questions.md) kaydını veya ilgili ADR'yi günceller ya da kullanıcıdan karar ister.

## İlk geliştirme hedefi

İlk çalışır dikey dilim:

`Kimlik ve şirket kapsamı → Muhasebe çekirdeği → Cari kartı ve vadeler → Açık kalem → Tahsilat → Allocation → Banka mutabakatı → Muhasebe fişi → Cari ekstre/aging → Audit → Web ekranı → Golden test ve restore`

Bu dilim başarılı olmadan satış, stok veya e-Fatura genişletilmez.

## v1.1 araştırma sonucu

- Dosya ve klasör yapısı değiştirilmedi; şartname hâlâ 56 Markdown dosyasından oluşur.
- Kaynak seçimi ve bulgular [kaynakçada](docs/references/SOURCES.md), eski planla ayrıntılı farklar [benchmark/boşluk analizinde](docs/references/ERP_BENCHMARK_AND_LOGO_GAP.md) kayıtlıdır.
- İlk dikey dilim artık tahsilatın kaydı, açık kaleme allocation’ı ve banka ekstresiyle reconciliation’ını ayrı durumlar olarak kanıtlar.
- Her mali rapor aynı as-of kesiminde kaynak belge → alt defter → kontrol hesabı → GL zincirine drill-down ve sıfır fark mutabakatı vermelidir.
- Go-live teknik bitiş değildir; paralel kapanış, kullanıcı eğitimi, süreç sahipliği ve 30/60/90 günlük fayda ölçümü release kapsamındadır.

## v1.2 master plan sonucu

- Kök [MASTER_PLAN.md](MASTER_PLAN.md) ile MP-00–MP-10 program fazları, giriş/çıkış kapıları ve ilk 20 uygulama işi tanımlandı.
- Codex'in her görevde bütün paketi değil, risk ve görev türüne göre gerekli belgeleri okuması için yönlendirme matrisi eklendi.
- Program planı ile tek işe ait yaşayan görev planı ayrıldı; karmaşık işler master fazı ve kapısına bağlandı.
- Definition of Ready, risk sınıfları, minimum test kapıları, blokaj politikası ve varsayılan devam et davranışı tanımlandı.
- v1.2 tasarım tabanı 57 Markdown dosyasından oluşur; yaşayan görev ve karar kayıtları eklendikçe repository dosya sayısı artabilir.
