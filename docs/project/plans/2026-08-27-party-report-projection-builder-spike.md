# Party report projection builder spike

- **Amaç:** Parties tarafından yayımlanan bitemporal source batch'ini, kaynak tablolara erişmeden doğrulanmış cari ekstre ve aging domain modellerine dönüştürmek.
- **Master fazı:** MP-03 Foundation / backlog 18 reporting spike.
- **Requirement:** RPT-PARTY-001, RPT-PARTY-002, RPT-INV-001, SEC-TEN-001.
- **Risk:** Yüksek — para işareti, tarih kesimi veya restriction kanıtının yanlış yorumlanması finansal raporu değiştirir.

## Sınırlar

Builder yalnız `Parties.Contracts` ve `Reporting.Domain` kullanır. Kaynak tablo okumaz, projection yazmaz, permission/API/job veya firma aging politikasını seçmez. Source contract açık kalem başlangıç olayının `source_type`, `effective_date` ve UTC `recorded_at` kanıtlarını taşır; due date işlem tarihi olarak varsayılmaz. Restriction kanıtı `Unavailable` ise aging üretimi fail-closed kalır.

## Plan ve sonuç

| Adım | Kanıt | Durum |
|---|---|---|
| Source contract bitemporal origin kanıtları | Cut sonrası origin reddi | completed |
| Statement normalization ve deterministik sıra | Open item + impact, exact closing cross-foot | completed |
| Aging normalization | Remaining total korunuyor, unavailable evidence reddediliyor | completed |
| Atomic-publication compatible pair | Statement ve aging aynı report code/generation kesiminde, construction-time cross-foot | completed |
| Mimari kapı | Reporting.Application yalnız contract + domain bağımlılığı | completed |

## Doğrulama

- `dotnet restore KaguERP.slnx --use-lock-file` geçti.
- Release build 0 warning/0 error geçti.
- Domain/contract harness 60 check geçti.
- Architecture harness 19 source project için geçti.

## Açık sınır ve sıradaki adım

Gerçek Parties adapter'ı henüz yazılamaz: mevcut `party_account` şeması receivable/payable yönünü ve explicit opening exposure kanıtını taşımıyor. Bu değerler tahmin edilmedi. Builder artık statement ve aging'i tek report definition/generation kesiminde çift olarak üretip `PartyStatementAgingCrossFoot` invariantını dönüşten önce uygular. Sıradaki güvenli teknik iş projection job orchestration sınırıdır.
