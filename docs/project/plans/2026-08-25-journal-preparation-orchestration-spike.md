# MP-03 Journal Preparation Orchestration Technical Spike

- **Amaç:** Authoritative dönem kapısı, posting permission/candidate, source reservation, validated draft, audit ve outbox işlemlerini tek caller-owned PostgreSQL transaction bileşeninde birleştirmek.
- **Master fazı ve kapısı:** MP-03 / backlog 20.
- **Risk sınıfı:** R4 — kısmi mali hazırlık, yetki atlama ve yanlış posted iddiası.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **İlgili requirement ID'leri:** `API-003`, `ACC-INV-004/005/006/007/008`, `ACC-PER-003`.
- **Definition of Ready sonucu:** Non-posted preparation orchestration için koşullu geçer; approval, API idempotency ve posted ledger kapsam dışıdır.

## Kapsam

### Dahil

- Accounting Application request/result sözleşmesi.
- Accounting Infrastructure katmanında transaction-bound PostgreSQL orchestrator; audit ve outbox yazıcıları transaction-bound callback olarak composition sınırından alınır.
- Authoritative period → candidate → reservation → draft → audit → outbox sırası.
- Commit/rollback ve permission/closed-period negatif gerçek DB kanıtı.

### Dahil değil

- Transaction commit sahipliği, posted journal, GL balance, API endpoint ve approval.

## Değişmezler ve güvenlik sınırları

- Orchestrator transaction açmaz veya commit etmez.
- Audit context scope/actor/tenant request ile birebir uyumlu olmalıdır.
- Üretilen olay `journal-draft-prepared`; `posted` değildir.
- Her hata caller rollback'iyle tüm hazırlık kayıtlarını geri alır.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Application request/result contract | Build | completed |
| 2 | Transaction-bound orchestrator | Integration commit | completed |
| 3 | Rollback ve negatif authorization/period | Integration negative | completed |
| 4 | Full repository kapısı | Full verify | completed |

## Tamamlanma kanıtı

- [x] Tek commit reservation, non-posted draft, audit ve outbox fact'lerini tam birer kez üretir.
- [x] Rollback hiçbir preparation fact'i bırakmaz.
- [x] Yetkisiz/kapalı dönem hiçbir persistence üretmez.
- [x] Full verify geçer; commit/push yapılmaz.

## Ara doğrulama kanıtı

- `dotnet build KaguERP.slnx --no-restore`: geçti, 0 warning / 0 error.
- `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj --no-restore`: geçti, 0 warning / 0 error.
- Architecture checks: 14 source project geçti.
- `scripts/test-db.ps1`: mevcut PostgreSQL üzerinde migration checksum, RLS, commit/rollback, permission ve kapalı dönem kontrolleri geçti.
- `scripts/verify.ps1`: .NET build, 55 domain check, API contract, 14-project architecture, web lint/typecheck/6 test/build, mevcut ve boş PostgreSQL, Keycloak, isolated restore ve Android lint/unit/instrumentation assemble kapıları geçti.
- Orchestrator transaction açmaz, commit etmez ve posted journal/GL üretmez.
