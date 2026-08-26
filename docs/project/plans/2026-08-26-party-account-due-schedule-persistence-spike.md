# MP-03 Party Account and Due Schedule Persistence Technical Spike

- **Amaç:** Party identity shell, company/currency scoped party account ve immutable due schedule'ı PostgreSQL'de exact-total garantisiyle kalıcılaştırmak.
- **Master fazı:** MP-03 / cari ilk dikey dilim.
- **Risk:** R4 — yanlış company/party scope, mutable bakiye ve eksik taksit toplamı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `PARTY-DUE-001`, `PARTY-DUE-002`, `PARTY-OI-001`.

## Sınır

Bu dilim kişi/unvan/VKN/IBAN alanı, payment-term hesaplama, allocation, mutable remaining balance, FX veya public API tanımlamaz. `party` yalnız ileride expand edilecek kimlik kabuğudur.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Party identity/account scope persistence | Migration 0018 | completed |
| 2 | Immutable due schedule header/line | Migration 0018 + PostgreSQL writer | completed |
| 3 | Deferred exact-total DB guard | Owner-tamper commit rejection | completed |
| 4 | RLS, privileges, writer ve integration | Real PostgreSQL gate | completed |

## Tamamlanma kanıtı

- Aynı kaynak olay/sürüm için ilk immutable sonuç döndürülür; header özeti veya herhangi bir taksit snapshot alanı değişirse `DUE_SCHEDULE_SOURCE_CONFLICT` üretilir.
- İki ayrı PostgreSQL connection'ı aynı source/version için yarıştığında yalnız ilk transaction kazanır; bekleyen işlem commit sonrasında aynı immutable schedule kimliğini döndürür.
- Deferred constraint trigger, satır sayısı ve original amount toplamını header ile tam eşleştirir; 100 GBP header'a 99 GBP satır yazılan owner-tamper transaction'ı commit edilemez.
- Başka company scope'undan okuma sıfır satır döndürür. Runtime rolü SELECT/INSERT yapabilir, UPDATE/DELETE yapamaz; RLS zorlanmıştır.
- `dotnet build tests/Architecture/KaguERP.ArchitectureChecks.csproj --configuration Release --no-restore`: 0 uyarı, 0 hata.
- Application Control güvenli architecture host üzerinden gerçek PostgreSQL integration kapısı: geçti.

Bu spike payment-term üretim politikasını, açık kalem remaining projeksiyonunu, allocation/unallocation, FX ve public API'yi bilinçli olarak sonraki dilimlere bırakır; MP-03 bu nedenle `proposed` kalır.
