# MP-03 Party Report Source Contract Technical Spike

- **Amaç:** Reporting'in Parties tablolarını doğrudan okumadan bitemporal open-item ve impact snapshot'ı alacağı dar, provider-independent contract'ı tanımlamak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — source ownership ihlali, örtük sign/opening/restriction varsayımı veya farklı cut'ların karışması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `PARTY-OI-001`, `PARTY-OI-002`, `RPT-INV-001`, `RPT-PARTY-001`, `RPT-PARTY-002`.

## Sınır

Contract sorgu uygulaması değildir. Explicit opening exposure, balance side, source watermark/checksum ve restriction evidence taşır. Mevcut kaynakta dispute/block kanıtı yoksa `Unavailable` zorunludur; Reporting bunu `Clear` sayamaz.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Independent `Parties.Contracts` project | Architecture | completed |
| 2 | Scoped bitemporal request ve immutable source batch | Contract checks | completed |
| 3 | Explicit restriction-unavailable ve lineage boundaries | Negative checks | completed |

## Tamamlama kanıtı

- `Parties.Contracts`, source module Domain/Infrastructure ve Reporting'e bağımlı olmayan yayımlanmış dar yüzeydir.
- Query tenant/company/party account/effective as-of/UTC recorded cutoff taşır; batch ayrıca control account, balance side, currency, explicit opening, watermark ve SHA-256 lineage içerir.
- Open-item fact original/remaining/due/source kimlikleri ile yalnız requested cut içinde kalan immutable impact fact'lerini taşır.
- Restriction evidence `Unavailable` ayrı durumdur ve testte `Clear` durumuna çevrilmeden korundu.
- Non-UTC cutoff ve cutoff sonrası impact negatif testte reddedildi; 59 domain/contract check ve 18-project architecture kapısı geçti.
- Contract canonical payload checksum üretimi tamamlandı: scope, kesimler, opening, watermark ve sıralanmış open-item/impact fact'leri length-framed SHA-256 girdisidir. Eşdeğer replay aynı hash'i, değişen lineage farklı hash'i üretir; impact koleksiyonu defensive copy ile korunur. Gerçek Parties adapter'ı sonraki dilimdir.
