# MP-03 Open-item Capacity Concurrency Guard Technical Spike

- **Amaç:** Paralel allocation/write-off etkilerinin due-line original amount kapasitesini aşmasını PostgreSQL'de engellemek.
- **Master fazı:** MP-03 / backlog 15; backlog 16 teknik ön koşulu.
- **Risk:** R4 — write skew ile negatif remaining veya original amount üstü settlement.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `PARTY-INV-001`, `PARTY-OI-001`, `PARTY-OI-002`.

## Sınır

Guard aynı due-line üzerindeki immutable impact netlerini korur. Payment kullanılabilir kapasitesi, allocation onayı, write-off yetkisi, FX ve GL bu dilimde seçilmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Due-line lock + net capacity DB guard | Migrations 0020-0021 | completed |
| 2 | Over-capacity owner tamper rejection | Real PostgreSQL | completed |
| 3 | Two-connection write race | Concurrency integration | completed |

## Tamamlanma kanıtı

- Runtime writer due-line kimliğine transaction-scoped advisory lock alır; bekleyen command kilit sonrası güncel committed history ile değerlendirilir.
- DB trigger allocation ve write-off netlerini ayrı ayrı negatif olmayan, birlikte due-line original amount'ı aşmayan aralıkta tutar.
- 60 GBP due-line üzerinde iki connection ile yarışan 40 ve 30 GBP allocation'dan ilki commit oldu; ikincisi `ck_open_item_impact_capacity` ile reddedildi.
- 40 GBP due-line'a doğrudan 41 GBP write-off yazan schema-owner tamper testi aynı constraint ile reddedildi.
- Runtime'a due-line UPDATE yetkisi verilmedi. Trigger'ın dar `SELECT FOR UPDATE` ihtiyacı, sabit `pg_catalog, party` search path ve execute revoke korunarak ileri migration 0021 ile security-definer yapıldı.

Payment usable capacity, approval, FX ve GL etkisi bu guard'ın iddiası değildir; backlog 16 ve MP-03 `proposed` kalır.
