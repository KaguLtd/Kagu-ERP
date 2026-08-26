# MP-03 Authoritative Due Schedule Loader Technical Spike

- **Amaç:** Immutable vade planını PostgreSQL'den company scope içinde domain modeli olarak yeniden kurmak.
- **Master fazı:** MP-03 / backlog 15.
- **Risk:** R4 — cross-company veri sızıntısı veya eksik/bozuk taksit snapshot'ının sessiz kabulü.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `PARTY-DUE-001`, `PARTY-DUE-002`.

## Sınır

Loader salt okunurdur. Remaining balance, allocation, payment-term üretimi, FX, public API veya başka modül tablosuna doğrudan erişim eklemez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Scope-bound header/line load | Infrastructure | completed |
| 2 | Domain invariant ile yeniden doğrulama | Integration | completed |
| 3 | Missing/cross-company fail-closed | Real PostgreSQL | completed |

## Tamamlanma kanıtı

- Loader header ve taksitleri aynı caller-owned transaction içinde, tenant/company/due-schedule kimliğiyle okur.
- Satırlar `DueScheduleLine.Create`, bütün plan `ValidatedDueSchedule.Create` üzerinden yeniden doğrulanır; bozuk veya eksik snapshot sessizce kabul edilmez.
- Gerçek PostgreSQL testi 40 + 60 = 100 GBP planını bütün immutable alanlarıyla yeniden kurdu; başka company scope'unda aynı ID `null` döndü.
- Release derlemesi 0 uyarı/0 hata ve Application Control güvenli gerçek DB integration kapısı geçti.

Bu salt-okunur loader allocation/write-off politikası veya mutable remaining alanı eklemez. MP-03 `proposed` kalır.
