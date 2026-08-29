# MP-03 Reconciliation Proposal Persistence Technical Spike

- **Amaç:** Domain-validated many-to-many reconciliation proposal'ını approved/reconciled sonuç üretmeden immutable PostgreSQL snapshot'ına taşımak.
- **Master fazı:** MP-03 / backlog 17 teknik ön koşulu.
- **Risk:** R4 — cross-scope match, statement/movement kapasite aşımı ve proposal snapshot bozulması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `BNK-REC-001`, `BNK-REC-002`.

## Sınır

Proposal approval, tolerance, scoring, maker-checker, settlement, correction, period lock, payment mutation ve GL kapsam dışıdır. Internal movement caller-supplied immutable capacity snapshot'ıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Immutable proposal/match snapshot | Migration 0024 | completed |
| 2 | Deferred scope/capacity cross-foot | Owner-tamper | completed |
| 3 | Idempotent writer, RLS ve privileges | Real PostgreSQL | completed |

## Tamamlanma kanıtı

- Proposal header treasury account/currency/match count; her match statement line ile movement version/direction/usable amount snapshot'ını immutable taşır.
- Deferred DB guard statement account/currency/direction bağını, header satır sayısını, statement absolute amount ve proposal içi movement usable-capacity toplamlarını cross-foot eder.
- 125.50 GBP statement üzerinde 100 GBP proposal geçti; owner-tamper 126 GBP match commit'te `ck_reconciliation_proposal_snapshot` ile reddedildi.
- Aynı proposal ID aynı içerikte ilk sonucu döndürür, değiştirilmiş match `RECONCILIATION_PROPOSAL_CONFLICT` üretir.
- Forced RLS, cross-company negatif okuma ve runtime UPDATE/DELETE yasağı gerçek PostgreSQL'de geçti.

Bu tablo bir öneridir; approved/reconciled fact değildir. Approval, tolerance, correction ve GL politikası seçilmeden sonuç durumuna çevrilmez; MP-03 `proposed` kalır.
