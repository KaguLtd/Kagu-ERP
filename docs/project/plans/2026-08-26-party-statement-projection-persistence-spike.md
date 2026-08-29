# MP-03 Party Statement Projection Persistence Technical Spike

- **Amaç:** Doğrulanmış cari ekstre sonucunu projection generation manifestine append-only ve exact cross-foot ile bağlamak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — karışık veri kesimi, değiştirilebilir rapor sonucu veya satır/header toplam farkı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-002`, `RPT-INV-005`, `RPT-INV-006`, `RPT-PARTY-001`.

## Sınır

Bu dilim yalnız önceden doğrulanmış Reporting domain ekstresini saklar. Parties tablolarını okumaz; source-to-sign mapping, opening balance acquisition, permission, aging, account mapping, job ve API politikası seçmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Generation-bound immutable statement/line schema | Migration 0026 | completed |
| 2 | Idempotent exact-payload writer | Integration | completed |
| 3 | Deferred line-count/running/closing cross-foot, RLS ve privilege | Real PostgreSQL | completed |

## Tamamlama kanıtı

- `0026_party_statement_projection.sql`, statement header ve deterministik sıralı normalized event satırlarını projection generation manifestine composite FK ile bağlar.
- Writer aynı statement ID ve birebir payload için ilk immutable sonucu döndürür; değişen opening/closing içeriği typed conflict üretir.
- Deferred DB guard line count, her satırın running exposure değeri ve final closing exposure toplamını exact `numeric(20,4)` ile cross-foot eder; eksik satırlı owner-tamper commit'i reddedildi.
- Forced RLS altında cross-company satır sayısı sıfır kaldı; runtime `SELECT`/`INSERT` dışındaki mutation yetkileri yoktur.
- Source-to-sign mapping, opening acquisition, Parties query adapter, permission, aging, job ve API bu dilimde seçilmedi.
