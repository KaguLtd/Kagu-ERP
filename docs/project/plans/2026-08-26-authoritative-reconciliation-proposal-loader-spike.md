# MP-03 Authoritative Reconciliation Proposal Loader Technical Spike

- **Amaç:** Immutable reconciliation proposal/match snapshot'ını PostgreSQL'den Treasury domain modeline yeniden kurmak.
- **Master fazı:** MP-03 / backlog 17 teknik ön koşulu.
- **Risk:** R4 — eksik/bozuk participant snapshot kabulü veya cross-company banka verisi sızıntısı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `BNK-REC-001`, `BNK-REC-002`.

## Sınır

Loader salt okunurdur ve yalnız proposal snapshot döndürür. Approval, tolerance, reconciled state, correction veya GL üretmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Header/match/statement authoritative load | Infrastructure | completed |
| 2 | Domain invariant ile yeniden doğrulama | Integration | completed |
| 3 | Missing/cross-company fail-closed | Real PostgreSQL | completed |

## Tamamlanma kanıtı

- Loader header, persisted statement satırları ve movement-capacity snapshot'larını aynı transaction ve company scope içinde yükler.
- Statement identity/hash/parser, movement version/direction/capacity, match ve proposal domain fabrikalarından yeniden geçirilir.
- 100 GBP match'li proposal bütün immutable participant alanlarıyla yeniden kuruldu; başka company scope'unda aynı ID `null` döndü.
- Release derlemesi ve gerçek PostgreSQL integration kapısı geçti.

Loader yalnız proposal döndürür; approval veya reconciled state kanıtı üretmez. MP-03 `proposed` kalır.
