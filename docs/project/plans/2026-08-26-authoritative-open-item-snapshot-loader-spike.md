# MP-03 Authoritative Open-item Snapshot Loader Technical Spike

- **Amaç:** Persisted due-line ve immutable impact history'den explicit effective-date/recorded-cutoff için remaining snapshot türetmek.
- **Master fazı:** MP-03 / backlog 15.
- **Risk:** R4 — late-recorded olay sızıntısı, mutable bakiye otoritesi veya cross-company okuma.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `PARTY-OI-001`, `PARTY-OI-002`.

## Sınır

Salt-okunur loader bütün immutable geçmişi domain invariantlarına verir; remaining DB'de saklanmaz. Public API, authorization permission adı, ödeme politikası, FX ve GL kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Due-line + impact history authoritative load | Infrastructure | completed |
| 2 | Effective/recorded as-of derivation | Real PostgreSQL | completed |
| 3 | Missing/cross-company fail-closed | Negative integration | completed |

## Tamamlanma kanıtı

- Loader due-line ve bütün immutable impact history'yi aynı transaction ve tenant/company scope içinde yükleyip domain invariantlarından yeniden geçirir.
- 40 GBP due-line üzerinde 20 GBP allocation, unallocation'ın recorded cutoff'undan önce 20 GBP remaining; counter kaydedildikten sonra 40 GBP remaining üretir.
- Remaining, allocated ve written-off değerleri yalnız explicit effective date + recorded cutoff üzerinden türetilir; DB'de mutable bakiye saklanmaz.
- Başka company scope'unda aynı due-line kimliği `null` döner. Gerçek PostgreSQL integration ve Release derleme kapıları geçti.

Public API/permission, payment authority, approval, FX ve GL composition kapsam dışıdır; MP-03 `proposed` kalır.
