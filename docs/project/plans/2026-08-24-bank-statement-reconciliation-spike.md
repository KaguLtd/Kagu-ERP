# MP-03 Bank Statement and Reconciliation Technical Spike

## Goal

Establish canonical imported-statement-line identity and immutable many-to-many reconciliation proposal boundaries without selecting a bank adapter, approval tolerance or accounting policy.

- Master phase/backlog: MP-03 / item 17
- Risk: R4 — duplicate bank evidence, cross-scope matching and over-reconciliation
- Status: Completed locally — MP-03 UAT acceptance remains at the master phase
- Requirements: `BNK-INV-003`, `BNK-STMT-001`, `BNK-REC-001`, `BNK-REC-002`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. Bank profiles, statement formats, currencies, tolerance, approval ownership, fees/FX and GL policy remain open or `atanmadı`; therefore this slice validates caller-supplied normalized evidence and proposals but does not import, approve, settle or post anything.

## Scope

Included:

- Canonical statement-line external identity scoped by tenant, company and treasury account.
- Immutable normalized line draft with signed decimal amount, currency, booking/value dates, UTC recorded time, raw-object SHA-256 and parser version.
- Duplicate statement-line identity and tenant-scoped line-ID rejection.
- Immutable internal movement capacity snapshot and many-to-many reconciliation match proposal.
- Same-scope/account/currency validation, pair uniqueness and aggregate capacity checks on both sides.
- Boundary, duplicate, ordering, immutability and architecture checks.

Excluded:

- File upload/parsing, malware scanning, encryption, provider adapters and bank-specific fingerprints.
- Statement opening/closing balance, sequence and file-level control totals.
- Match scoring, tolerance, approval, maker-checker, period locks and approved reconciliation lifecycle.
- Payment mutation, bank settlement, allocation, fees, FX, suspense events, persistence, API, outbox or GL posting.

## Milestones

- [x] Record requirement traceability and deferred policy boundaries.
- [x] Add canonical statement-line identity and immutable normalized line draft.
- [x] Add duplicate-safe deterministic statement-line set.
- [x] Add scoped internal-movement capacity and many-to-many reconciliation proposal.
- [x] Prove scope, currency, direction, amount, capacity, uniqueness, ordering and immutability behavior.
- [x] Pass full local repository verification.

## Verification Evidence

- Debug and Release builds passed with zero warnings and zero errors; locked restore remained current.
- Domain quality harness passed all 44 checks. New checks cover normalized identity, signed decimal amount, UTC/raw-hash/parser evidence, tenant-scoped deduplication, incoming/outgoing direction, cross-scope/currency/account rejection, many-to-many matching, pair uniqueness, aggregate capacity, deterministic ordering and collection immutability.
- Architecture harness passed for all 11 source projects; the new statement and reconciliation types remain inside independent Treasury.Domain.
- Web lint, TypeScript typecheck, Vitest (2 tests) and production build passed.
- Real PostgreSQL migration idempotency and tenant/company RLS checks passed; Keycloak permission-scope smoke, isolated restore/migration/scope/outbox/auth smoke and Android lint/unit/instrumentation build gates passed.
- Full `scripts/verify.ps1` completed successfully. Its formatting gate first identified two whitespace-only issues; both were corrected before the successful rerun.
- No provider adapter, external-key derivation, tolerance, score, approval, bank settlement, payment/allocation mutation, fee/FX event or GL policy was inferred.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- External identity/fingerprint derivation belongs to a versioned provider/profile adapter.
- Tolerance, score threshold and automatic/manual approval require approved reconciliation policy.
- Approved-match correction must use an append-only counter-event under `BNK-INV-005`; this proposal slice does not authorize approval or mutation.
- Persistent deduplication and concurrent approval require PostgreSQL constraints and transaction-level tests.
- No commit, push or PR will be created until explicitly requested by the user.

## 31 Ağustos 2026 implementation continuation

`DEC-MP01-022` ile önceki deferred politika sınırı kapandı. Sıradaki R4 dikey dilim; immutable proposal'ı exact subject/version approval evidence'ına bağlayacak, hazırlayanı final onaydan ayıracak, ilk sürümde `0,00` toleransı zorunlu kılacak ve eşleşen gerçek payment kimliğini yeni payment üretmeden transit→ana banka GL kapanışına taşıyacaktır.

Bu devam dilimi banka/provider formatı, gerçek company hesap eşlemesi, credential, masraf/faiz/iade/chargeback olayı veya production provisioning seçmez. Bunlar ayrı source event ya da production-only mapping olarak kalır. Yeni çalışma MP test politikasına göre dar domain/DB/compile kanıtıyla ilerler; birleşik repository, restore, web ve Android regresyonu MP validating kapısında çalışır.

### Continuation milestones

- [x] Treasury Application katmanını ve company-scoped `treasury.reconciliation.approve` kapısını ekle.
- [x] Exact proposal/version workflow kanıtı, gerçek maker ve tek farklı manager quorum'unu zorunlu kıl.
- [x] Statement ve movement tarafında `0,00` tolerans/exact capacity invariantını ekle.
- [x] `0041` ile append-only approval sonucu, global participant locks, forced RLS ve deferred approval/payment cross-foot guard ekle.
- [x] Persistence writer'da persisted proposal ve gerçek payment snapshot'ını fail-closed doğrula; retry'da ilk immutable sonucu döndür.
- [x] Approved reconciliation kaynağını TRY identity-rate transit→ana banka journal posting composition'a aynı transaction içinde bağla.
- [x] Yabancı para reconciliation için payment-date transit functional tutarı ile statement-date banka functional tutarı arasındaki realized FX gain/loss'u ayrı hesap satırı ve kur/policy snapshot'larıyla tanımla.
- [x] Gerçek PostgreSQL migration/RLS/concurrency/golden tekrarını MP validating test paketinde geçir.

Dar doğrulama: Treasury Application, Treasury Infrastructure, Migrator, Unit, Integration ve Architecture projeleri ayrı ayrı `0 warning / 0 error` derlendi; `git diff --check` temizdir. Yalnız `reconciliation` unit filtresi iki domain kontrolünden proposal kontrolünü çalıştırdı, yeni Application kontrolü ise yeniden üretilen BuildingBlocks DLL'ine uygulanan Windows Application Control `0x800711C7` nedeniyle runtime'a başlayamadı. Güvenlik politikası bypass edilmedi; runtime ve DB sonucu MP validating kapısında açık kanıttır.

Transit posting devam kanıtı (31 Ağustos 2026): Yeni Treasury.Contracts fact'i yalnız append-only approved header ve kilitli statement/payment katılımcılarından, aktif tenant/company scope ve aynı transaction içinde üretilir. Accounting factory her statement booking date için ayrı canonical source üretir; incoming hareket banka kontrol hesabını borçlandırıp payment kimlikli incoming-transit satırlarını alacaklandırır, outgoing hareket bunun tersini yapar. Proposal-level yönetici onayı bütün statement journal'larına exact approval subject olarak taşınır. Bootstrap orchestration approval persistence, authoritative fact load ve bütün journal prepare/post işlemlerini caller'ın tek PostgreSQL transaction'ında birleştirir ve statement-command kümesini exact doğrular.

Her journal satırı authoritative exchange-rate ve rounding snapshot'ı taşır. Bu dilim yalnız transaction currency = functional currency ve identity rate kabul eder; böylece çoklu payment satırında gizli yuvarlama/kur farkı oluşmaz. USD/EUR/GBP transit kapanışı, payment-date ile statement-date functional değer farkını ayrı FX ekonomik olayına bağlayan takip dilimi tamamlanmadan fail-closed kalır. İlgili Contracts, Accounting Application, Treasury Infrastructure, Bootstrap, Unit, Integration ve Architecture derlemeleri `0 warning / 0 error` geçti. Dar `transit` runtime kontrolü Windows Application Control `0x800711C7` nedeniyle başlayamadı; gerçek PostgreSQL same-transaction rollback/idempotency/golden testi MP validating paketine bırakıldı.

Realized FX devamı (1 Eylül 2026): Approved Treasury contract artık her matched payment'ın tam transaction/functional kapasitesini, payment-date rate kimlik/sürümünü ve AwayFromZero rounding policy kimlik/sürümünü taşır. Batch, aynı payment'ın bütün match parçalarının transaction kapasitesini exact tüketmesini ve tek functional currency kullanmasını zorunlu kılar. Accounting, transit satırını payment-date snapshot'ından; banka satırını statement booking-date snapshot'ından üretir. Incoming/outgoing yönüne göre fark hard-code hesap kodu olmadan mapped realized FX gain veya loss hesabına, functional-currency identity snapshot'lı ayrı journal satırı olarak yazılır. Payment birden fazla statement'a bölündüğünde parça functional toplamı original payment functional amount'a exact eşit değilse residual varsayılmaz ve posting fail-closed durur.

Treasury Contracts/Infrastructure, Accounting Application, Bootstrap, Unit, Integration ve Architecture derlemeleri `0 warning / 0 error` geçti. Bu kanıt standalone sonradan girilen kur farkı düzeltme olayı değildir; approved reconciliation journal'ındaki deterministik realized FX satırıdır. `0042` gerçek PostgreSQL migration/backfill/tamper testi ile reconciliation same-transaction golden hâlâ MP validating paketindedir.

MP-validating kapanışı (2 Eylül 2026): Birleşik `scripts/verify.ps1` koşusu `67` domain ve `22` source-project mimari/API kontrolüyle birlikte populated + empty PostgreSQL/RLS/integration, `0042` apply/idempotency, Keycloak, izole restore ve web/Android kapılarının tamamını geçti. Kapanış sırasında yakalanan eksik migration catalog kaydı, Bootstrap→Treasury.Domain doğrudan referansı ve restore ortam değişkeni bağımlılığı düzeltildikten sonra tam paket sıfır hatayla yeniden çalıştı. Bu teknik plan tamamlandı; master faz yalnız golden UAT kabul kaydını bekler.
