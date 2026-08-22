# MP-03 Muhasebe Kaynak Tekilliği Teknik Spike Planı

- **Amaç:** Firma muhasebe politikası veya persistence davranışı varsaymadan, bir kaynak olayın aynı şirket ve posting amacı için canonical kimliğini üretmek ve aynı doğrulama kümesindeki duplicate journal niyetini reddetmek.
- **Master fazı ve kapısı:** MP-03 / business implementation öncesi politika bağımsız ACC-INV-005 spike.
- **Risk sınıfı:** R4 — finansal idempotency sınırına temas eder; gerçek posting, DB uniqueness ve concurrency garantisi kapsam dışıdır.
- **Durum:** validating
- **Sahip:** Roller `DEC-MP01-019` gereği atanmadı; kullanıcı politika bağımsız teknik ilerlemeyi onayladı.
- **Başlangıç / hedef tarih:** 2026-08-22 / ilk source-identity dilimi aynı çalışma içinde.
- **İlgili requirement ID'leri:** ACC-INV-005, DATA muhasebe constraintleri ve idempotency test standardı.
- **Etkilenen belgeler/modüller:** Accounting Domain, domain unit checks ve MP-03 teknik ilerleme kaydı.
- **Okunan zorunlu belgeler:** `AGENTS.md`, `MASTER_PLAN.md`, `src/Modules/Accounting/AGENTS.md`, `docs/modules/09-accounting-general-ledger.md`, `docs/00-foundation/04-data-architecture.md`, `docs/00-foundation/07-cross-cutting-workflows.md`, `docs/quality/01-testing-and-quality-strategy.md`.
- **Definition of Ready sonucu:** conditional-pass for technical spike. Canonical source identity ve process içi duplicate doğrulaması bağlayıcı sözleşmelerden çıkarılabilir. PostgreSQL unique index, transaction, active/reversal durumu, API idempotency response ve gerçek posting `DEC-MP01-001`–`010`, `012` açıkken uygulanmaz.

## Master plan ilişkisi

Bu plan, tamamlanan dengeli journal draft spike'ının ardından ACC-INV-005 için ikinci küçük teknik dilimdir. MP-03 fazını business implementation için başlatmaz ve production idempotency garantisi vermez.

## Kapsam

### Dahil

- Tenant, company, source type, source event ve posting purpose alanlarından immutable canonical identity.
- Metin alanlarında yalnız çevre boşluklarının kaldırılması; case veya iş anlamı dönüşümü yok.
- Doğrulanmış journal draft üzerinde identity'nin açıkça taşınması.
- Aynı identity'yi taşıyan iki taslağın tek doğrulama kümesinde reddedilmesi.
- Farklı company, source veya posting purpose değerlerinin ayrı identity kabul edilmesi.
- Boundary, duplicate ve immutability kontrolleri.

### Dahil değil

- `journal_entry` tablosu, migration veya unique index.
- “Aktif journal” ve reversal/correction yaşam döngüsü.
- Paralel transaction, lock, retry veya API idempotency kaydı.
- Posting rule seçimi, hesap kodu, dönem, kur, yuvarlama, vergi veya onay politikası.
- Posted journal, audit veya outbox yazımı.

## Değişmezler ve güvenlik sınırları

- Identity tenant ve company kapsamını zorunlu taşır; cross-company kaynaklar birleşmez.
- Source type ve posting purpose boş olamaz, yalnız `Trim()` ile canonical hale gelir.
- Aynı canonical identity ikinci kez doğrulanırsa `JOURNAL_SOURCE_DUPLICATE` ile reddedilir.
- Duplicate kontrolü yalnız verilen in-memory küme için kanıttır; production tekilliğinin yerine geçmez.
- Kümeye alınan draft koleksiyonu dışarıdan değiştirilemez.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Canonical posting identity | Scope/source/purpose boundary ve equality kontrolleri | completed |
| 2 | Duplicate draft set kontrolü | Aynı identity ret; farklı company/purpose kabul | completed |
| 3 | Immutability ve regresyon | Input mutation etkisiz; ACC-INV-001 kontrolleri yeşil | completed |
| 4 | Root ve CI kapısı | Tam yerel verify ve remote CI | validating — yerel geçti; remote CI bekliyor |

## Test planı

- **Unit/boundary:** boş UUID/metin, çevre boşluğu canonicalization, identity equality.
- **Invariant:** aynı identity ve farklı rule/line içeriği duplicate olarak reddedilir.
- **Metamorphic:** farklı company veya purpose tekillik alanını ayırır.
- **Immutability:** kaynak array değişse de doğrulanmış küme değişmez.
- **DB/concurrency/API:** Bu spike'ta uygulanmaz; persistence diliminde gerçek PostgreSQL unique index ve paralel test zorunludur.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-08-22 | Process içi kontrol production yarışını engellemez | ACC-INV-005 tam kapanmaz | İsim açıkça teknik spike; DB/concurrency sonraki kapıda |
| 2026-08-22 | Case-normalization iş anlamını değiştirebilir | Farklı source sınıfları yanlış birleşebilir | Yalnız çevre boşluğu kaldırılır; case korunur |
| 2026-08-22 | Active/reversal tanımı business kararı ister | Etkin sonuç seçimi uygulanamaz | Yaşam döngüsü kapsam dışı |

## İlerleme günlüğü

### 2026-08-22

- ACC-INV-005, veri mimarisi unique-key sözleşmesi ve idempotency test standardı yeniden okundu.
- Persistence garantisi iddiasında bulunmayan canonical identity + duplicate draft-set dilimi seçildi.
- `JournalPostingIdentity`, doğrulanmış draft identity bağı ve immutable `ValidatedJournalDraftSet` uygulandı.
- Domain kontrolleri 8'den 11'e çıktı; canonical equality, duplicate rejection, company/purpose ayrımı ve collection immutability geçti.
- Tam `scripts/verify.ps1` kapısı geçti: Release build/format, domain/architecture, web, PostgreSQL/RLS, Keycloak auth/audit, izole restore ve Android lint/unit/instrumentation APK derlemesi başarılıdır.

## Tamamlanma kanıtı

- [x] Canonical identity boundary/equality kontrolleri geçer.
- [x] Duplicate ve ayrı-scope senaryoları geçer.
- [x] Kümeye dış mutation yapılamaz.
- [x] Önceki ACC-INV-001 regresyon kontrolleri geçer.
- [x] Tam yerel verify geçer.
- [ ] Remote CI geçer.
- [x] Plan ve master teknik durum notu günceldir.
