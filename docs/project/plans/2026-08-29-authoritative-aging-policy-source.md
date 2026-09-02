# Authoritative aging policy source

- **Amaç:** Şirketin sürümlü calendar-day yaşlandırma politikasını effective date ve recorded cutoff kesiminde seçip Party rapor projection job'una authoritative kaynak olarak vermek.
- **Master fazı:** MP-03 / backlog 18.
- **Requirement:** RPT-INV-001, RPT-PARTY-002, SEC-TEN-001, DEC-MP01-009.
- **Risk:** R4 — yanlış şirket, gelecek bilgi veya belirsiz politika sürümüyle cari yaşlandırma yayımlama.
- **Durum:** completed.

## Sınır ve karar

Politika tanımı Reporting'e ait sürümlü, append-only bir iş kuralıdır; source modül verisini değiştirmez. Her şirket tek bir policy kimliği altında ardışık sürümler yayımlar. Bir rapor kesimi, `effective_from <= effective_as_of` ve `recorded_at <= recorded_cutoff` koşullarını sağlayan en yüksek sürümü seçer. Projection generation bu tanımı mevcut immutable policy snapshot tablolarına kopyalar.

Onaylı başlangıç aralıkları test kanıtında `future`, `due-now`, `1-30`, `31-60`, `61-90`, `91-120`, `121+` olarak temsil edilir; değerler uygulama koduna varsayılan olarak hard-code edilmez. Politika authoring endpoint'i, production permission kodu, onay UI'ı ve Worker scheduling bu dilimin dışındadır.

## Milestone'lar

| # | Milestone | Done when | Durum |
|---|---|---|---|
| 1 | İleri migration | Header/bucket tabloları, tam aralık guard'ı, append-only runtime yetkisi ve forced RLS vardır | completed |
| 2 | PostgreSQL source adapter | Effective/recorded kesiminde doğru sürüm ve bucket'lar domain snapshot'ına yüklenir | completed |
| 3 | Job bileşimi | Gerçek Party→aging→GL golden akışı fixed fixture yerine authoritative source kullanır | completed |
| 4 | Güvenlik ve migration kanıtı | Cross-company, gelecek-kayıt, version/policy stream, coverage ve privilege negatifleri gerçek PostgreSQL'de geçer | completed |
| 5 | Repository kapıları | Release build, format, unit, architecture ve DB integration yeşildir | completed |

## Kanıt

- `0036_authoritative_aging_policy` ve caller RLS'ini koruyan security-invoker advisory-lock düzeltmesi `0037_aging_policy_stream_guard_privilege` mevcut test DB'sine ileri uygulandı; son yükseltme `1/0` idempotent kaldı.
- Doğrulanmış ayrı boş `kagu_erp_empty_codex_20260829_a7c4f1` veritabanında `37/0` migration ve tam PostgreSQL tenant/company RLS integration paketi geçti; geçici DB testten sonra silindi.
- Version 2 effective date öncesinde version 1 seçildi; version 2'nin recorded timestamp'ından önceki cutoff aynı effective date'te version 1'i görmeye devam etti.
- Company scope dışı çağrı application gate'te reddedildi; başka şirket RLS kesiminde mevcut policy görünmedi.
- Version atlama, şirket policy kimliğini değiştirme ve bucket aralığında bir günlük boşluk named PostgreSQL constraint'leriyle reddedildi.
- Runtime rolü definition tablolarında SELECT/INSERT taşıyor; UPDATE/DELETE taşımıyor.
- Runtime rolü ardışık version 3 header ve yedi bucket'ı kendi şirket scope'unda yazıp commit edebildi; future-effective sürüm 2026 rapor kesimini değiştirmedi.
- Gerçek posted `75 GBP` Party kaynağının projection job'u fixed policy fixture yerine `PostgresPartyAgingPolicySource` kullanarak statement/aging/GL setini atomik yayımladı ve replay yeni fact üretmedi.
- Release build `0 warning/error`; 63 domain check, 20-project architecture/API check, format ve diff kapıları geçti.
- Test için başlatılan Kagu ERP PostgreSQL kümesi doğrulama sonunda durduruldu; production veya kullanıcı verisi değiştirilmedi.

## Rollback / compensation

Migration geri alınırken tablo düşürülmez. Yeni source adapter deploy'dan çıkarılıp projection job önceki güvenli davranışına döndürülür; politika bulunmayan kesimler zaten fail-closed kalır. Kullanılmayan şema ancak bağımlılık ve veri incelemesinden sonra ayrı, onaylı forward compensation migration'ıyla ele alınır.

## Açık sınırlar

- Açılış bakiyesinin due date/open-item kimliği ayrıca karara bağlıdır; bu görev o ekonomik olayı yaşlandırılabilir varsaymaz.
- Politika değişikliği yetkisi ve yönetici onay/audit komutu public API diliminde tamamlanacaktır.
