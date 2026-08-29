# MP-03 Report Projection Generation Manifest Technical Spike

- **Amaç:** Finansal report slice ve projection generation lineage'ını source checksum ile append-only PostgreSQL manifestine taşımak.
- **Master fazı:** MP-03 / backlog 18 teknik ön koşulu.
- **Risk:** R4 — farklı veri kesimlerinin karışması veya yeniden üretilemeyen rapor generation'ı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-005`, `RPT-INV-006`.

## Sınır

Manifest rapor rakamlarını business authority yapmaz ve source modül tablolarını okumaz. Report definition authoring, projection writer/job, account mapping, aging policy, permission, export ve UI kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Immutable slice/lineage/dimension manifest | Migration 0025 | completed |
| 2 | Exact dimension cross-foot ve idempotent writer | Integration | completed |
| 3 | RLS, privileges ve changed-payload conflict | Real PostgreSQL | completed |

## Tamamlama kanıtı

- `0025_report_projection_generation.sql`, report scope, definition version, effective as-of, UTC cutoff/generated timestamp, currency, generation reason, source watermark aralığı, checksum, generation actor ve dimension kesimini append-only saklar.
- Aynı generation kimliği ve aynı lineage idempotent replay olur; değişen checksum typed conflict üretir.
- Deferred constraint trigger, manifestteki dimension sayısını commit anında exact cross-foot eder.
- Gerçek PostgreSQL testi cross-company RLS izolasyonunu, runtime `SELECT`/`INSERT` ile sınırlı yetkileri ve owner-tamper dimension-count reddini doğruladı.
- Bu dilim bir rapor projection job'ı veya muhasebe otoritesi üretmez; yalnız daha sonra üretilecek projection'ın yeniden üretilebilir lineage manifestidir.
