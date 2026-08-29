# MP-03 Authoritative Party Report Cross-Foot Composition Technical Spike

- **Amaç:** Persisted statement ve aging projection'larını aynı transaction/company scope içinde yükleyip exact domain cross-foot üretmek.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — farklı hesap, generation, data cut veya toplamların birlikte sunulması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-002`, `RPT-PARTY-001`, `RPT-PARTY-002`.

## Sınır

Composition yalnız authoritative Reporting projection loader'larını kullanır. Source query, permission policy, job, API veya drill-down davranışı üretmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Same-transaction statement + aging load | Infrastructure composition | completed |
| 2 | Same-slice exact closing/remaining cross-foot | Integration | completed |
| 3 | Missing/cross-company fail-closed | Real PostgreSQL | completed |

## Tamamlama kanıtı

- Composition iki authoritative loader'ı aynı caller-owned transaction ve company scope içinde çalıştırır.
- Domain cross-foot tenant/company, report code/version, effective as-of, cutoff, generation, currency, dimensions, party/control account ve balance-side eşitliğini fail-closed doğrular.
- Statement closing exposure ile aging total remaining exact decimal eşleşti.
- Missing veya cross-company projection birleşimi `null` döndürdü.
- Gerçek PostgreSQL integration, solution build, domain/architecture ve format kapıları geçti.
