# MP-03 Authoritative Payment Economic-event Loader Technical Spike

- **Amaç:** Immutable payment/rate snapshot'ını PostgreSQL'den company scope içinde domain modeli olarak yeniden kurmak.
- **Master fazı:** MP-03 / backlog 16 teknik ön koşulu.
- **Risk:** R4 — eksik kur/source kanıtının kabulü veya cross-company payment sızıntısı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `BNK-PAY-001`, `BNK-PAY-002`, `BNK-INV-002` same-currency alt kümesi.

## Sınır

Loader Treasury-owned ve salt okunurdur. Approved/posted/settled/reconciled state, allocation kullanılabilirliği, FX, GL veya public API üretmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Scope-bound immutable snapshot load | Infrastructure | completed |
| 2 | Domain invariant ile yeniden doğrulama | Integration | completed |
| 3 | Missing/cross-company fail-closed | Real PostgreSQL | completed |

## Tamamlanma kanıtı

- Loader payment kimliğiyle aynı caller-owned transaction ve tenant/company scope içinde bütün payment, canonical source ve rate snapshot alanlarını yükler.
- PostgreSQL değerleri `TreasuryCurrencyCode`, `SameCurrencyPaymentRateSnapshot` ve `ValidatedPaymentEconomicEventDraft` fabrikalarından yeniden geçirilir; bozuk snapshot sessiz kabul edilmez.
- 100 GBP payment bütün immutable alanlarıyla yeniden kuruldu; başka company scope'unda aynı payment ID `null` döndü.
- Release derlemesi 0 uyarı/0 hata ve gerçek PostgreSQL integration kapısı geçti.

Loader payment lifecycle state veya allocation usable-capacity kanıtı üretmez; MP-03 `proposed` kalır.
