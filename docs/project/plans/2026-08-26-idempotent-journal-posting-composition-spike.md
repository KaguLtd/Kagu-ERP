# MP-03 Idempotent Journal Posting Composition Technical Spike

- **Amaç:** API idempotency acquire→prepare→post→audit/outbox→completed-response akışını tek caller-owned PostgreSQL transaction'ında birleştirmek.
- **Master fazı:** MP-03 / final posted response composition.
- **Risk:** R4 — retry'nin ikinci mali sonuç üretmesi veya idempotency cevabının posting fact'lerinden ayrı commit olması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-001`, `ACC-INV-005`, `WFL-INV-002`, `API-005`.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Posted response idempotency composition | Build | completed |
| 2 | Replay ve changed-payload conflict | Real PostgreSQL | completed |
| 3 | Posting/outbox hatasında tam rollback ve temiz retry | Real PostgreSQL | completed |
| 4 | Repository doğrulama bileşenleri ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- `PostgresIdempotentJournalPostingOrchestrator`, canonical request hash'iyle idempotency kaydını posting'den önce acquire eder; yeni istekte prepare→post→audit/outbox zincirini çalıştırıp HTTP 201 posted response snapshot'ıyla aynı transaction içinde tamamlar.
- Completed replay source loader, approval veya posting zincirini yeniden çalıştırmadan ilk posted journal kimliği ve timestamp'ini döndürür. Aynı anahtarda farklı expected source version `IDEMPOTENCY_KEY_REUSED` ile fail-closed davranır.
- Gerçek PostgreSQL testinde 15 migration uygulanmış izole Kagu ERP kümesi üzerinde ilk sonuç, replay, changed-payload conflict ve zorlanmış posted-outbox hatasında idempotency kaydı ile posted journal'ın birlikte rollback'i geçti.
- Repository doğrulama bileşenleri 26 Ağustos 2026'da ayrı ayrı geçti: .NET build 0 uyarı/0 hata, 58 domain kontrolü, architecture/API, `dotnet format`, web lint/typecheck/6 test/build, gerçek PostgreSQL integration ve Android lint/unit/instrumentation build. `scripts/verify.ps1` aynı .NET ve web aşamalarını geçti ancak kurulu Docker Desktop executable'ına rağmen Linux engine pipe'ı açılmadığı için `docker compose ps` aşamasında çevresel olarak durdu; DB ve Android kapıları bağımsız çalıştırıldı.

## Açık sınır

Bu dilim public endpoint, yasal numaralama, reversal persistence veya gerçek kaynak belge/workflow write command'ı açmaz.
