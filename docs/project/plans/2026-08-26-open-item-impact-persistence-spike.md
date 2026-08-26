# MP-03 Open-item Impact Persistence Technical Spike

- **Amaç:** Open-item allocation/write-off etkilerini mutable remaining alanı oluşturmadan append-only PostgreSQL defterinde saklamak.
- **Master fazı:** MP-03 / backlog 15; backlog 16 için yalnız teknik ön koşul.
- **Risk:** R4 — scope sızıntısı, aynı etkinin iki kez yazılması veya karşı olayın aslıyla uyuşmaması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `PARTY-OI-001`, `PARTY-OI-002`, `PARTY-INV-004`.

## Sınır

Bu dilim caller-supplied, domain-validated immutable impact snapshot'ını saklar. Treasury payment doğruluğunu, allocation onayını, write-off yetkisini, FX/GL üretimini veya public API'yi kanıtlamaz. Başka modül tablosuna FK/doğrudan erişim eklenmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Append-only impact ledger ve due-line bağı | Migration 0019 | completed |
| 2 | Exact counter-event DB guard | Owner-tamper test | completed |
| 3 | Idempotent transaction-bound writer | Integration | completed |
| 4 | Forced RLS ve runtime privilege | Real PostgreSQL | completed |

## Tamamlanma kanıtı

- Impact event due-schedule line'a aynı tenant/company kapsamında bağlıdır; remaining amount kolonu yoktur.
- Allocation/unallocation payment kimliği taşır, write-off türleri taşımaz. Karşı olay yalnız doğru original türüne bağlanabilir ve party account, due line, payment, currency ve amount birebir eşleşir.
- Aynı event ID ve aynı snapshot retry'da ilk sonucu döndürür; farklı immutable içerik `OPEN_ITEM_IMPACT_CONFLICT` üretir.
- 20 GBP allocation ve exact unallocation gerçek PostgreSQL'de geçti; 19 GBP owner-tamper counter event `ck_open_item_exact_counter` ile reddedildi.
- Forced company RLS ve runtime SELECT/INSERT varlığı ile UPDATE/DELETE yokluğu doğrulandı.

Bu kanıt payment'ın Treasury tarafında var veya kullanılabilir olduğunu, business approval'ı ya da GL posting'ini doğrulamaz. Bunlar sahip/politika kararları verilene kadar backlog 16 kapsamında blokludur; MP-03 `proposed` kalır.
