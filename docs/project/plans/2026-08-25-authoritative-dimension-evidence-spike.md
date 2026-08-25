# MP-03 Authoritative Dimension Evidence Technical Spike

- **Amaç:** Posting-rule version'a bağlı required dimension setini PostgreSQL'den yükleyip journal satırlarında fail-closed doğrulamak.
- **Master fazı:** MP-03 / sıra 2 ve backlog 20.
- **Risk:** R4 — gerekli mali boyutun sessiz varsayılanla veya eksik kaydedilmesi.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-007`, `API-003`.
- **Definition of Ready:** Immutable evidence için geçer; dimension authoring, varsayılan politika ve approval kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Requirement set/line migration | Current/empty DB | completed |
| 2 | Authoritative loader | Build/integration | completed |
| 3 | Missing/cross-scope/read-only negatifleri | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- `0011` mevcut DB'ye uygulandı ve ikinci migration koşusu 0 değişiklikle geçti.
- Temiz DB üzerinde 11 migration ve gerçek entegrasyon seti geçti.
- Exact posting-rule version requirement seti yüklendi; eksik required dimension domain invariantıyla reddedildi.
- Requirement set yokluğu ve cross-company görünmezliği fail-closed kanıtlandı.
- Runtime requirement set/line tablolarında yalnız SELECT yetkisine sahip.
- Full repository verify .NET, web, current/empty DB, Keycloak, isolated restore ve Android kapılarıyla geçti.
