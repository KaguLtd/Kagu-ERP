# MP-03 Authoritative Party Aging Projection Loader Technical Spike

- **Amaç:** Persisted aging projection'ını generation ve policy snapshot'larıyla aynı transaction/company scope içinde domain modeline yeniden kurmak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — farklı data cut/policy veya eksik item setinden aging sonucu üretimi.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-002`, `RPT-INV-005`, `RPT-INV-006`, `RPT-PARTY-002`.

## Sınır

Loader yalnız Reporting-owned manifest, policy ve aging projection tablolarını okur. Source query, tenant default policy, permission, job veya API davranışı üretmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Transaction-bound manifest/policy/header/item reconstruction | Infrastructure loader | completed |
| 2 | Exact domain ve bucket-summary round-trip | Integration | completed |
| 3 | Missing/cross-company lookup fail-closed | Real PostgreSQL | completed |

## Tamamlama kanıtı

- Loader aging header, generation manifesti, aging policy snapshot'ı ve ordinal item satırlarını caller-owned transaction içinde yükler.
- Scope, as-of/cutoff, currency, hesaplar, policy ID/version, item tutar/tarih/flag alanları birebir domain round-trip edildi.
- `TotalRemaining` ve bucket summaries persisted kopyadan okunmadı; authoritative item ve policy snapshot'larından domain tarafından yeniden türetildi.
- Forced RLS altında cross-company lookup `null` döndü.
- Gerçek PostgreSQL integration, solution build, domain/architecture ve format kapıları geçti.
