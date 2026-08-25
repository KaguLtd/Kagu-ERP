# MP-03 Canonical Journal Source Port Technical Spike

- **Amaç:** Dış komutun journal draft/snapshot göndermesini engelleyen, source identity'den server-side canonical draft yükleyen transaction-bound application portu kurmak.
- **Master fazı:** MP-03 / posting pipeline adım 5.
- **Risk:** R4 — istemci veya caller tarafından mali satır, kural, kur ya da hesap enjeksiyonu.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-005`, `API-003`, `API-005`.
- **Definition of Ready:** Generic source port için geçer; gerçek belge türü ve posting rule politikası kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Draft taşımayan command ve canonical source sonucu | Build | completed |
| 2 | Permission-first transaction-bound source loader composition | Integration | completed |
| 3 | Source identity/scope mismatch negatifleri | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- `JournalPreparationCommand` yalnız trusted scope/audit, canonical source identity ve server-generated işlem kimliklerini taşır; journal satırı, hesap, kur veya posting rule snapshot'ı taşımaz.
- Transaction-bound source loader portu canonical draft ve chart version üretir. Permission loader çağrısından önce denetlenir; loader sonucu komuttaki source identity ile birebir eşleşmezse `JOURNAL_SOURCE_IDENTITY_MISMATCH` ile fail-closed davranır.
- Gerçek PostgreSQL testi canonical kaynakla atomik preparation ve farklı kaynak döndüren adapter için sıfır journal fact kanıtladı.
- `scripts/verify.ps1` 25 Ağustos 2026 tarihinde .NET, web, mevcut/boş PostgreSQL, restore, RLS, auth ve Android kapılarının tamamında geçti.

## Açık sınırlar

Port gerçek kaynak belge şeması veya posting rule seçimi tanımlamaz. Satış, satın alma, ödeme ve manuel fiş adapter'ları ilgili mali politika ve modül contract'ı onaylandıktan sonra uygulanacaktır. Public endpoint ve posted journal/GL persistence kapsam dışıdır.
