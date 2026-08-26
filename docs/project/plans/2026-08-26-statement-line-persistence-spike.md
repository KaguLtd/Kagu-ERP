# MP-03 Normalized Statement-line Persistence Technical Spike

- **Amaç:** Domain-validated normalize banka ekstresi satırını canonical external identity ile append-only PostgreSQL'e taşımak.
- **Master fazı:** MP-03 / backlog 17 teknik ön koşulu.
- **Risk:** R4 — aynı banka satırının ikinci mali kanıt üretmesi veya cross-company sızıntı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `BNK-INV-003`, `BNK-STMT-001`.

## Sınır

Bu dilim dosya upload/parser, banka profili, external-key türetimi, raw object storage, opening/closing control total, approval, reconciliation veya GL üretmez. Yalnız caller-supplied doğrulanmış snapshot'ı saklar.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Immutable normalized line | Migration 0023 | completed |
| 2 | External identity idempotent writer | Infrastructure | completed |
| 3 | Concurrency, RLS ve privileges | Real PostgreSQL | completed |

## Tamamlanma kanıtı

- Statement import/line ve treasury account kimlikleri, canonical external identity, signed amount/currency, booking/value date, raw SHA-256 ve parser version immutable saklanır.
- Aynı external identity ve aynı snapshot retry'da ilk sonucu döndürür; farklı line kimliği `STATEMENT_LINE_IDENTITY_CONFLICT` üretir.
- İki connection aynı external identity için yarıştığında yalnız ilk satır oluşur; bekleyen transaction ilk immutable sonucu alır.
- Forced company RLS, cross-company negatif okuma ve runtime SELECT/INSERT varlığı ile UPDATE/DELETE yokluğu gerçek PostgreSQL'de geçti.

External key türetimi adapter/profile sorumluluğudur. Bu persistence import dosyası güvenliği, approval, reconciliation veya GL posting iddiası taşımaz; MP-03 `proposed` kalır.
