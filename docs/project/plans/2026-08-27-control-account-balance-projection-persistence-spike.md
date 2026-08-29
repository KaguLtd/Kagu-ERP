# MP-03 Control-Account Balance Projection Persistence Technical Spike

- **Amaç:** Doğrulanmış subledger ve GL balance snapshot'larını aynı projection generation'a append-only bağlamak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — farklı data cut veya kontrol hesaplarından yanıltıcı mutabakat.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-003`, `RPT-INV-005`, `RPT-INV-006`, `RPT-CTRL-001`, `RPT-CTRL-002`.

## Sınır

Bu dilim caller tarafından doğrulanmış balance snapshot'ını saklar. Account mapping, sign convention, source query, tolerance, permission, job ve API politikası seçmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Generation-bound immutable subledger/GL snapshot schema | Migration 0029 | completed |
| 2 | Exact idempotent writer ve reconciliation reconstruction | Integration | completed |
| 3 | Arithmetic constraint, RLS ve append-only privilege | Real PostgreSQL | completed |

## Tamamlama kanıtı

- `0029_control_account_balance_projection.sql`, her generation/control-account/ledger-side için immutable snapshot'ı tekilleştirir.
- Writer birebir retry için ilk sonucu döndürür; değişen balance payload typed conflict üretir.
- Loader manifesti aynı transaction/company scope içinde yükleyip domain arithmetic invariantlarını yeniden uygular; reconciliation loader aynı kesimdeki subledger ve GL snapshot'larını exact karşılaştırır.
- DB `opening + debits - credits = closing` eşitliğini `numeric(20,4)` ile zorunlu kılar; owner-tamper reddedildi.
- Forced RLS, cross-company fail-closed ve runtime `SELECT`/`INSERT` ile sınırlı yetki gerçek PostgreSQL'de doğrulandı.
