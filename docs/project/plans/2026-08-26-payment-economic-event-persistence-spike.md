# MP-03 Payment Economic-event Persistence Technical Spike

- **Amaç:** Domain-validated same-currency payment draft'ını immutable ve source-unique PostgreSQL snapshot'ına taşımak.
- **Master fazı:** MP-03 / backlog 16 teknik ön koşulu.
- **Risk:** R4 — duplicate nakit niyeti, kur snapshot kaybı ve cross-company sızıntı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `BNK-PAY-001`, `BNK-PAY-002`; `BNK-INV-002` same-currency alt kümesi.

## Sınır

Bu kayıt onay, posted banka/kasa hareketi, bankaya gönderim, settlement, reconciliation veya GL değildir. Party allocation tablosuna doğrudan erişmez ve allocation için payment kullanılabilirliği kanıtı sayılmaz.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Immutable payment/rate snapshot | Migration 0022 | completed |
| 2 | Source-unique idempotent writer | Infrastructure | completed |
| 3 | RLS, privilege ve concurrency | Real PostgreSQL | completed |

## Tamamlanma kanıtı

- Payment kimliği, party/treasury account kimlikleri, direction, transaction/functional tutar, effective/recorded zaman, canonical source/purpose ve bütün identity-rate snapshot alanları immutable saklanır.
- Aynı canonical source/purpose ve aynı payload retry'da ilk payment ID'sini döndürür; ikinci payment kimliği `PAYMENT_SOURCE_CONFLICT` üretir.
- İki PostgreSQL connection'ının aynı source için yarışında yalnız ilk transaction event oluşturdu; bekleyen ikinci transaction ilk sonucu aldı.
- Forced RLS cross-company okumayı sıfır satıra indirdi; runtime SELECT/INSERT yapabilir, UPDATE/DELETE yapamaz.
- Treasury.Infrastructure ayrı modül projesidir; Party veya Accounting referansı yoktur. Mevcut sabitlenmiş Npgsql kullanılmış, yeni üçüncü taraf paket eklenmemiştir.

Bu snapshot approved/posted/settled/reconciled payment kanıtı değildir ve allocation usable capacity sağlamaz. Bu davranışlar açık lifecycle/workflow politikası olmadan etkinleştirilmez; MP-03 `proposed` kalır.
