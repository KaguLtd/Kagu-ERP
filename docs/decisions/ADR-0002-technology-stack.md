# ADR-0002 — Teknoloji Yığını

- Durum: Accepted
- Tarih: 2026-08-18
- Son doğrulama: 2026-08-19 — ERP veri/rapor/restore gereksinimleriyle

## Bağlam

Uzun ömürlü, tip güvenli, iyi test/telemetry ekosistemli web+Android ERP ve Linux self-hosting gerekir. Finansal transaction, PostgreSQL ve kurumsal kimlik entegrasyonu önceliklidir.

## Karar

- Backend: .NET 10 LTS, ASP.NET Core, C# nullable/analyzers.
- DB: PostgreSQL 18'in güncel minor'u; EF Core + gerektiğinde gözden geçirilmiş SQL.
- Web: React, TypeScript strict, Vite, shadcn/ui/Tailwind.
- Android: Kotlin, Jetpack Compose, Room/WorkManager.
- Kimlik: Keycloak OIDC.
- Edge: Caddy.
- Çalıştırma: Docker Compose.
- Gözlem: OpenTelemetry.
- Backup: pgBackRest + restic.

Tam sürümler repo oluşturma/release tarihinde destek tablolarıyla doğrulanıp lock/digest ile sabitlenir.

## Sonuçlar

- LTS ve yaygın ekosistem; Linux/containers uyumlu.
- Web ve Android ayrı platforma uygun deneyim; iş kuralı API'de ortak.
- Ekip C#, TypeScript, Kotlin ve PostgreSQL yetkinliği ister.
- Çoklu teknoloji dependency/patch takibini gerektirir.

## Reddedilenler

Tek bir cross-platform UI: ERP web yoğunluğu ve Android saha/offline ihtiyaçlarını aynı kalıba zorlar. SQL Server: Linux/self-host maliyeti ve PostgreSQL yetenekleri nedeniyle seçilmedi. Node backend: mümkün olsa da bu proje için .NET tip/transaction/operasyon standardı tercih edildi.

## Yeniden değerlendirme

EOL, kritik güvenlik/lisans sorunu, ekip sürdürülebilirliği veya ölçülmüş teknik engel. Moda veya küçük syntax tercihi migration gerekçesi değildir.

## v1.1 açıklaması

Araştırma teknoloji kararını değiştirmedi. PostgreSQL transaction, numeric, constraint, RLS, PITR ve gelişmiş sorgu özellikleri; .NET domain/application katmanları ve typed API; React/shadcn yoğun web workbench; Kotlin/Compose kontrollü saha/offline görevleri için uygundur.

Açık kaynak ERP’ler davranış/veri modeli referansıdır, runtime dependency değildir. Onlardan kod/schema alınması ayrı lisans ve clean-room incelemesi ister. UBL ve ISO 20022 adapter contract’ı versioned parser/fixture ile uygulanır; domain modeli dış şemaya bağlanmaz.

Yeni reporting store, broker veya cache bu yığına otomatik eklenmez. Kapanış/repost/report ölçümleri mevcut PostgreSQL worker/read-model sınırını aştığında ADR açılır.
