# MP-03 Authoritative Aging Policy Projection Loader Technical Spike

- **Amaç:** Persisted aging policy projection snapshot'ını aynı transaction/company/generation scope içinde domain modeline yeniden kurmak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — yanlış policy sürümü veya bucket sınırlarıyla rapor yeniden üretimi.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-006`, `RPT-PARTY-002`.

## Sınır

Loader yalnız Reporting-owned snapshot'ı okur; tenant default'u, policy approval'ı, source query, aging item veya API davranışı üretmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Transaction-bound policy/bucket reconstruction | Infrastructure loader | completed |
| 2 | Exact domain round-trip | Integration | completed |
| 3 | Missing/cross-company lookup fail-closed | Real PostgreSQL | completed |

## Tamamlama kanıtı

- Loader header ve ordinal sıralı bucket satırlarını caller-owned transaction içinde yükler.
- Policy ID/version ile her bucket code/minimum/maximum sınırı domain snapshot'ına birebir round-trip edildi; domain tam kapsam ve bitişiklik invariantlarını yeniden uygular.
- Forced RLS altında cross-company lookup `null` döndü.
- Gerçek PostgreSQL integration, solution build, domain/architecture ve format kapıları geçti.
