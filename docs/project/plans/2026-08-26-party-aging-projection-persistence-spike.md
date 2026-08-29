# MP-03 Party Aging Projection Persistence Technical Spike

- **Amaç:** Doğrulanmış aging item sonucunu aynı generation ve policy snapshot'ına append-only bağlamak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — aging toplamı, scope, data cut veya policy snapshot ayrışması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-002`, `RPT-INV-005`, `RPT-INV-006`, `RPT-PARTY-002`.

## Sınır

Bu dilim doğrulanmış aging item'larını saklar; bucket özetleri aynı policy ve item'lardan türetilir. Source query, dispute/block workflow, tenant default policy, permission, job ve API seçilmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Generation/policy-bound immutable aging header/items | Migration 0028 | completed |
| 2 | Exact idempotent writer | Integration | completed |
| 3 | Deferred item-count/remaining cross-foot, RLS ve privileges | Real PostgreSQL | completed |

## Tamamlama kanıtı

- `0028_party_aging_projection.sql`, aging header ve deterministik item snapshot'larını aynı generation ve aging policy snapshot'ına composite FK ile bağlar.
- Writer birebir retry için immutable ilk sonucu döndürür; değişen remaining item payload'ı typed conflict üretir.
- Deferred DB guard item count ve total remaining değerlerini exact `numeric(20,4)` ile cross-foot eder; silinmiş item owner-tamper commit'i reddedildi.
- Forced RLS altında cross-company görünürlük sıfırdır ve runtime yalnız `SELECT`/`INSERT` yetkisine sahiptir.
- Bucket summary ayrıca saklanmaz; aynı policy snapshot ve authoritative item'lardan domain tarafından türetilir.
