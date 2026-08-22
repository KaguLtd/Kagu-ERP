# MP-03 Muhasebe Çekirdeği Teknik Spike Planı

- **Amaç:** Firma ve KKTC muhasebe politikalarını varsaymadan, dengeli ve değişmez journal taslağı invariantını çalışır kod ve otomatik testle kanıtlamak.
- **Master fazı ve kapısı:** MP-03 / business implementation öncesi politika bağımsız teknik spike.
- **Risk sınıfı:** R4 — finansal çekirdeğe temas eder; gerçek posting, hesap planı ve production davranışı kapsam dışıdır.
- **Durum:** completed
- **Sahip:** Roller `DEC-MP01-019` gereği atanmadı; kullanıcı teknik ilerlemeyi onayladı.
- **Başlangıç / hedef tarih:** 2026-08-21 / ilk invariant dilimi aynı çalışma içinde.
- **İlgili requirement ID'leri:** ACC-INV-001, ACC-INV-002, DATA para/tarih/değişmezlik standartları.
- **Etkilenen belgeler/modüller:** Accounting Domain, domain unit checks, solution/CI ve MP-01/MP-02 karar kayıtları.
- **Okunan zorunlu belgeler:** `AGENTS.md`, `MASTER_PLAN.md`, `PLANS.md`, `docs/00-foundation/03-repository-and-code-structure.md`, `docs/00-foundation/04-data-architecture.md`, `docs/00-foundation/07-cross-cutting-workflows.md`, `docs/modules/03-party-current-accounts.md`, `docs/modules/09-accounting-general-ledger.md`, `docs/quality/01-testing-and-quality-strategy.md`.
- **Definition of Ready sonucu:** conditional-pass for technical spike. `DEC-MP01-001`–`010` ve `012` açık olduğu için business posting, golden expected sonuç, DB migration, API ve kullanıcı akışı blokludur. Decimal ve borç=alacak değişmezleri bağlayıcı olduğu için saf domain invariantı uygulanabilir.

## Master plan ilişkisi

MP-02 teknik platformu tamamlanmıştır. Bu plan MP-03 fazını production/business implementation olarak başlatmaz; karar kaydında açıkça izin verilen invariant/state-machine iskeletinin ilk küçük dilimidir. Gerçek hesap kodu, para politikası, dönem seçimi, kur, yuvarlama, vergi, onay veya posting kuralı eklenmez.

## Kapsam

### Dahil

- `Accounting.Domain` modül sınırı ve modül kuralları.
- ISO-benzeri üç harfli kodu yalnız biçimsel doğrulayan currency value object; resmi/izinli para kataloğu seçimi yok.
- Negatif olmayan ve aynı satırda yalnız borç veya yalnız alacak taşıyan decimal journal satırı.
- Tenant/company/source/effective-date/rule-version bağlamlı immutable journal draft.
- En az iki satır ve aynı currency içinde borç=alacak doğrulaması.
- Unit/boundary test harness'i ve root/CI doğrulamasına bağlama.

### Dahil değil

- PostgreSQL migration, posted journal persistence veya idempotency unique index.
- Hesap planı, gerçek hesap kodları, control account mapping veya posting rule seçimi.
- Dönem açık/kapalı kontrolü, kur/yuvarlama, vergi veya multi-currency hesaplama.
- Reversal/repost, manual journal onayı, API, web, Android ve rapor.
- Golden muhasebe sonucu veya “KKTC uyumlu” beyanı.

## Değişmezler ve güvenlik sınırları

- Para yalnız C# `decimal`; `double`/`float` kabul edilmez.
- Her satırda borç ve alacaktan yalnız biri pozitiftir; negatif veya sıfır/sıfır satır reddedilir.
- Journal toplam borç ve alacağı tam decimal eşit değilse doğrulanmış taslak oluşmaz.
- Tenant, company, source event, rule version ve account kimlikleri boş olamaz.
- Effective date ile recorded timestamp ayrı taşınır; recorded timestamp UTC olmalıdır.
- Oluşan doğrulanmış taslak satır koleksiyonunu dışarıdan değiştirmek mümkün değildir.
- Bu tip “posted” değildir ve persistence/authorization garantisi vermez.

## Tasarım

- **Domain:** `CurrencyCode`, `JournalAmount`, `JournalLineDraft` ve `ValidatedJournalDraft` değerleri; factory tüm invariantları atomik doğrular.
- **Veritabanı/migration:** Yok; sonraki business-ready dilime ertelendi.
- **Kaynak/GL etkisi:** Yalnız source event ve posting rule version kimliği taşınır; journal DB gerçeği veya outbox olayı üretilmez.
- **Tarih:** `DateOnly effectiveDate` ve UTC `DateTimeOffset recordedAt` ayrıdır.
- **Yetki/audit:** Uygulanmaz; hiçbir endpoint veya kayıt yazımı yoktur.
- **Deployment/rollback:** Yeni saf domain projesi ve test harness'i kaldırılarak geri alınabilir; veri değişikliği yoktur.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Accounting module/domain sınırı | Solution ve architecture check yeni projeyi doğru katmanda görür | completed |
| 2 | Decimal journal satırı invariantı | Negatif, çift taraflı ve boş satır boundary testleri | completed |
| 3 | Dengeli immutable journal draft | Balanced/imbalanced, scope/source/time/currency ve mutation testleri | completed |
| 4 | Root ve CI kapısı | Domain unit harness, Release build, format ve architecture geçer | completed — tam yerel root verify ve remote CI 6/6 geçti |

## Test planı

- **Unit:** Geçerli satır/taslak ve tüm validation sınırları.
- **Property/invariant:** Çok satırlı farklı decimal dağılımlarında debit=credit; tek cent/fraction farkında ret.
- **Architecture:** Accounting.Domain hiçbir Application/Infrastructure/API referansı almaz.
- **DB/API/E2E/Security/Migration/Restore:** Bu saf ve persistence içermeyen teknik spike için uygulanmaz; business diliminde zorunludur.
- **Golden accounting cycle:** Uzman kararları olmadığı için uygulanmaz ve başarılı sayılmaz.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-08-21 | Firma para, yuvarlama ve hesap planı kararları açık | Gerçek posting üretilemez | Yalnız biçim ve değişmezlik doğrulanır; politika eklenmez |
| 2026-08-21 | İsimli sahipler atanmadı | Uzman kabulü ve production bloklu | `DEC-MP01-019`; geliştirme sonunda yeniden değerlendirilecek |
| 2026-08-21 | Yeni test framework bağımlılığı eklemek gereksiz supply-chain yüzeyi yaratır | İlk invariant dilimi ağırlaşır | Mevcut executable quality-harness yaklaşımıyla bağımlılıksız unit checks |

## İlerleme günlüğü

### 2026-08-21

- Kullanıcının isimli sahipleri geliştirme sonuna erteleme kararı `DEC-MP01-019` olarak kayda hazırlandı.
- MP-02 teknik kanıtları tamamlandı; MP-03 business implementation blokajı korunarak ilk saf domain invariant dilimi seçildi.
- `Accounting.Domain` ve bağımlılıksız executable unit-check projesi solution ile root/CI doğrulamasına bağlandı.
- Release build 0 uyarı/0 hata ile geçti. 8 domain check ve 9 source project architecture check başarılı oldu; `dotnet format --verify-no-changes` temizdir.
- Windows PowerShell yerel `.env` okuması `ConvertFrom-StringData` ile güvenilir hale getirildi; HTTP Problem Details ve JDK stderr sürüm çıktısı PowerShell 5 uyumlu işlendi.
- `scripts/verify.ps1` eksiksiz geçti: locked restore, Release build/format, domain ve architecture checks, web lint/typecheck/test/build, PostgreSQL migration/RLS, Keycloak auth/audit, izole restore ve Android lint/unit/instrumentation APK derlemesi başarılıdır.
- İlk PR koşusunda beş teknik job geçti; Gitleaks bulgu üretmeden PR commit API'sinde `403` aldı. Workflow tokenına yalnız gerekli `pull-requests: read` izni eklendi; yazma izni verilmedi.
- Commit `c1c8f03` için GitHub Actions run `32455701366` içindeki clean bootstrap, backend/domain+architecture, web, Android, PostgreSQL/RLS/restore ve secret scan job'larının 6/6'sı geçti. Taslak PR: `#8`.

## Tamamlanma kanıtı

- [x] ACC-INV-001 ve satır sınırı testleri geçer.
- [x] Immutable doğrulanmış journal draft dış mutation kabul etmez.
- [x] Release build, format ve architecture check geçer.
- [x] Root verify domain unit harness'i ve tüm yerel kapıları çalıştırır.
- [x] Remote CI domain unit harness'i çalıştırır.
- [x] Açık business kararları kod içine sabitlenmemiştir.
- [x] Plan günlüğü ve master etkisi günceldir.
