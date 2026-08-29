# MP-03 Authoritative Party Statement Projection Loader Technical Spike

- **Amaç:** Persisted cari ekstre projection'ını generation manifestiyle aynı transaction/company scope içinde Reporting domain modeline yeniden kurmak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — farklı generation, scope veya sıralamadaki rapor satırlarının birleştirilmesi.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-002`, `RPT-INV-005`, `RPT-INV-006`, `RPT-PARTY-001`.

## Sınır

Loader yalnız Reporting-owned manifest ve statement projection tablolarını okur. Parties tablolarına erişmez; source-to-sign, opening, permission, aging veya API politikası seçmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Transaction-bound manifest/header/line reconstruction | Infrastructure loader | completed |
| 2 | Exact domain round-trip | Integration | completed |
| 3 | Missing/cross-company lookup fail-closed | Real PostgreSQL | completed |

## Tamamlama kanıtı

- Loader statement header'ı, generation manifestini ve deterministik line sırasını caller-owned transaction içinde yükler.
- Persisted scope, generation, hesaplar, balance side, opening/closing, normalize event alanları ve running exposure değerleri Reporting domain modeliyle birebir round-trip edildi.
- Domain reconstruction tüm statement ve report-slice invariantlarını yeniden uygular.
- Forced RLS altında cross-company lookup `null` döndü; source modül tablosu okunmadı.
- Gerçek PostgreSQL integration, solution build, domain/architecture ve format kapıları geçti.
