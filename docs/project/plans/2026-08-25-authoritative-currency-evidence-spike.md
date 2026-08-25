# MP-03 Authoritative Currency Evidence Technical Spike

- **Amaç:** Journal kur ve rounding snapshot'larını immutable PostgreSQL kanıtıyla birebir doğrulamak.
- **Master fazı:** MP-03 / sıra 2 ve backlog 20.
- **Risk:** R4 — kur/yuvarlama kanıtı değiştirme veya yeniden üretilemeyen fonksiyonel tutar.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-008`, `API-003`.
- **Definition of Ready:** Immutable evidence için geçer; kur kaynağı seçimi/import politikası ve approval kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Rate/rounding migration | Current/empty DB | completed |
| 2 | Authoritative exact-match loader | Build/integration | completed |
| 3 | Missing/tamper/scope/read-only negatifleri | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- `0012_currency_rounding_evidence` migration'ı immutable kur ve yuvarlama snapshot tablolarını `numeric(28,12)`, forced RLS ve runtime `SELECT` sınırıyla ekledi.
- Transaction-bound loader, draft içindeki snapshot'ları tenant/company kapsamında PostgreSQL kanıtıyla birebir eşleştiriyor; eksik, değiştirilmiş veya başka şirkete ait kanıtı typed fail-closed hatayla reddediyor.
- `scripts/test-db.ps1` mevcut veritabanında migration, exact-match, tamper, missing, cross-company ve read-only senaryolarıyla geçti.
- `scripts/verify.ps1` 25 Ağustos 2026 tarihinde 12 migration'lı mevcut ve boş PostgreSQL, restore, RLS, auth, .NET, web ve Android kapılarının tamamında geçti.

## Açık sınırlar

Bu dilim kur sağlayıcısı seçmez, oran yayımlamaz ve mali politika onayı vermez. Authoring/import, approval ve posted journal/GL persistence sonraki MP-03 dilimleridir.
