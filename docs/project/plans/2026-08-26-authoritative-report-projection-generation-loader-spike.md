# MP-03 Authoritative Report Projection Generation Loader Technical Spike

- **Amaç:** Persisted projection generation manifestini aynı transaction ve company scope içinde Reporting domain slice'ına yeniden kurmak.
- **Master fazı:** MP-03 / backlog 18 teknik ön koşulu.
- **Risk:** R4 — farklı scope, cutoff, version veya dimension kesimlerinin aynı rapor generation'ı gibi kullanılması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-005`, `RPT-INV-006`.

## Sınır

Loader yalnız Reporting-owned immutable manifesti okur. Source modül tablolarını, rapor rakamlarını, aging bucket politikasını, account mapping'i veya permission politikasını üretmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Transaction-bound manifest ve dimension reconstruction | Infrastructure loader | completed |
| 2 | Exact persisted lineage round-trip | Integration | completed |
| 3 | Missing/cross-company lookup fail-closed | Real PostgreSQL | completed |

## Tamamlama kanıtı

- Loader, caller-owned PostgreSQL transaction'ında header ve deterministik sıralı dimension satırlarını okuyup `FinancialReportSlice` üzerinden tüm domain invariantlarını yeniden uygular.
- Persisted report code/version, effective as-of, UTC cutoff/generated timestamp, currency, generation ID, reason, watermark, checksum ve actor alanları birebir round-trip edildi.
- Forced RLS altında başka company scope'u aynı generation ID için `null` döndürdü; source modül tablosu okunmadı.
- Gerçek PostgreSQL integration, solution build, domain/architecture ve format kapıları geçti.
