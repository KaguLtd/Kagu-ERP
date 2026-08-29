# MP-03 Aging Policy Projection Snapshot Technical Spike

- **Amaç:** Caller tarafından açıkça seçilmiş sürümlü calendar-day aging policy'yi projection generation'a immutable bağlamak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — bucket sınırı veya policy sürümü bilinmeyen aging sonucu.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-006`, `RPT-PARTY-002`.

## Sınır

Bu snapshot tenant için varsayılan policy seçmez ve policy approval otoritesi değildir. Yalnız generation sırasında caller'ın sunduğu, domain tarafından doğrulanmış policy kimliği/sürümü ve bucket aralıklarını kaydeder.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Generation-bound immutable policy/bucket schema | Migration 0027 | completed |
| 2 | Exact idempotent snapshot writer | Integration | completed |
| 3 | Full-range/contiguous bucket DB guard, RLS ve privileges | Real PostgreSQL | completed |

## Tamamlama kanıtı

- `0027_aging_policy_projection_snapshot.sql`, policy ID/version ve sıralı bucket snapshot'ını generation manifestine composite FK ile bağlar.
- Writer aynı generation ve birebir policy payload için idempotent replay döndürür; değişen policy version typed conflict üretir.
- Deferred DB guard bucket sayısını, `int.MinValue`–`int.MaxValue` tam kapsamını ve bitişik/non-overlap aralıkları doğrular; silinmiş bucket owner-tamper commit'i reddedildi.
- Forced RLS altında cross-company görünürlük sıfırdır ve runtime yalnız `SELECT`/`INSERT` yetkisine sahiptir.
- Snapshot tenant varsayılanı veya approval otoritesi değildir; yalnız generation'da kullanılan açık policy kanıtıdır.
