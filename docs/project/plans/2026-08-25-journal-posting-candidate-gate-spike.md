# MP-03 Journal Posting Candidate Gate Technical Spike

- **Amaç:** Aynı doğrulanmış journal taslağına ait scope, permission, dönem, hesap, boyut ve kur kanıtlarını tek fail-closed application kapısında birleştirmek.
- **Master fazı ve kapısı:** MP-03 / backlog 20 teknik posting ön koşulu.
- **Risk sınıfı:** R4 — yetkisiz veya eksik kanıtla mali kesinleştirme.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri şimdilik `atanmadı`.
- **Başlangıç / hedef tarih:** 25 Ağustos 2026 / 25 Ağustos 2026.
- **İlgili requirement ID'leri:** `API-003`, `ACC-INV-004`, `ACC-INV-006`, `ACC-INV-007`, `ACC-INV-008`, `ACC-PER-003`.
- **Etkilenen belgeler/modüller:** Accounting Application, Accounting Domain doğrulama zarfları, unit/architecture testleri.
- **Okunan zorunlu belgeler:** `MASTER_PLAN.md`, `PLANS.md`, Accounting `AGENTS.md`, `docs/modules/09-accounting-general-ledger.md`, `docs/00-foundation/04-data-architecture.md`, `docs/00-foundation/05-api-contracts.md`, `docs/00-foundation/07-cross-cutting-workflows.md`.
- **Definition of Ready sonucu:** Teknik, non-posted ve geri döndürülebilir kapı için koşullu geçer. Authoritative period lookup, approval policy ve gerçek posted persistence hâlâ blokajlıdır.

## Kapsam

### Dahil

- Accounting modülüne Application katmanı eklenmesi.
- `accounting.journal.post` permission ve tenant/company scope kontrolü.
- Hesap, boyut ve kur doğrulama zarflarının aynı draft instance'ına bağlı olduğunun kanıtlanması.
- GL ve hard/legal dönem kilitlerinin açık olması.
- Eksik, farklı taslağa ait, kapalı dönemli ve yetkisiz adayların fail-closed reddi.

### Dahil değil

- Posted journal üretimi veya durumu.
- DB period/account lookup, row lock, approval ve maker-checker.
- API endpoint, idempotency response veya numaralama.
- Reopen/backdate politikası.

## Değişmezler ve güvenlik sınırları

- Aday nesnesi mali bakiye veya posted state üretmez.
- Permission şirket kapsamlı ve exact ordinal kodla kontrol edilir.
- Caller tarafından birleştirilen bağımsız/stale doğrulama zarfları kabul edilmez.
- Dönem seti authoritative değildir; yalnız teknik snapshot kanıtıdır.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Accounting Application proje sınırı | Architecture build | completed |
| 2 | Fail-closed posting candidate gate | Unit boundary checks | completed |
| 3 | Doküman ve tam repository kapısı | Full verify | completed |

## Test planı

- Unit: doğru scope/permission ve tüm doğrulamalarla aday kabulü.
- Security: permission yokluğu ve başka company scope reddi.
- Invariant: farklı draft validation zarfı ve kapalı/eksik dönem reddi.
- Architecture: Application yalnız Domain ve ortak Application building block sınırında kalır.
- DB/API/E2E: Bu non-persistent dilimde uygulanmaz; posted davranış iddiası yoktur.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-08-25 | Period snapshot effective-date aralığı taşımıyor | Gerçek posting yetkisi verilemez | Aday explicit non-posted kalır / sahip atanmadı |

## İlerleme günlüğü

- 2026-08-25: Sözleşmeler okundu; doğrudan posted persistence yerine non-posted application candidate sınırı seçildi.
- 2026-08-25: Accounting Application projesi ve candidate kapısı eklendi. 55 unit/application kontrolü ve 14 source proje architecture kontrolü geçti.
- 2026-08-25: Full verify; .NET Release build/format, web lint/typecheck/6 test/build, mevcut ve boş PostgreSQL migration/RLS/integration, Keycloak permission smoke, izole restore ve Android lint/unit/instrumentation build kapılarından geçti.

## Tamamlanma kanıtı

- [x] Kabul ve negatif unit kontrolleri geçer.
- [x] Architecture ve full repository doğrulaması geçer.
- [x] Doküman sınırı posted durum iddia etmez.
- [x] Commit/push yapılmadı.
