# MP-03 Permission-First Party Report Query Gate Technical Spike

- **Amaç:** Authoritative Party report composition'ını DB erişiminden önce explicit required permission ve company scope ile fail-closed korumak.
- **Master fazı:** MP-03 / backlog 18; backlog 19 ön koşulu.
- **Risk:** R4 — UI görünürlüğünü authorization saymak veya yetkisiz rapor varlığını yan kanalla sızdırmak.
- **Durum:** completed.
- **Sahip:** Ürün ve güvenlik sahipleri `atanmadı`.
- **Requirement ID:** `IAM-INV-002`, `RPT-INV-001`, `RPT-INV-002`, `RPT-PARTY-002`.

## Sınır

Gate permission kodu seçmez; versioned report definition tarafından verilen non-empty kodu zorunlu kılar. Endpoint, permission kataloğu, audit composition ve response DTO kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Parametric permission-first query request | Infrastructure composition | completed |
| 2 | Allowed authoritative cross-foot load | Integration | completed |
| 3 | Denied request before DB access | Negative check | completed |

## Tamamlama kanıtı

- Query gate explicit, non-empty required permission code ve company scope ister; kodu kendisi seçmez.
- Fixture permission bulunan scope, aynı transaction içindeki authoritative statement-aging cross-foot'u yükledi.
- Permission bulunmayan scope'a var olmayan statement/aging ID'leri verildiğinde loader çağrısından önce `PARTY_REPORT_QUERY_DENIED` üretildi; resource-existence yan kanalı açılmadı.
- Spike tamamlandığında açık olan `DEC-MP01-012` daha sonra onaylandı. Production follow-up, immutable `reporting.party-account.view` kodunu `party.account.detail` version `1` tanımına bağladı; endpoint ve audit kanıtı [production query planında](2026-08-29-party-report-api-query.md) tamamlandı.
- Gerçek PostgreSQL integration, solution build, domain/architecture ve format kapıları geçti.
