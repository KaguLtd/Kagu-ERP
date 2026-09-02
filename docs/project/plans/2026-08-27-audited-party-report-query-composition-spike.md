# MP-03 Audited Party Report Query Composition Technical Spike

- **Amaç:** Permission-first Party report query sonucunu ortak append-only audit writer ile caller-owned transaction'a bağlamak.
- **Master fazı:** MP-03 / backlog 18; backlog 19 ön koşulu.
- **Risk:** R4 — yetkili/yetkisiz finansal rapor erişiminin denetim izi olmaması veya spoofed audit context.
- **Durum:** completed.
- **Sahip:** Ürün ve güvenlik sahipleri `atanmadı`.
- **Requirement ID:** `IAM-INV-002`, `RPT-INV-001`, `RPT-INV-002`.

## Sınır

Reporting platform audit tablosuna doğrudan yazmaz; caller ortak transaction-bound appender'ı sağlar. Production permission code, endpoint, response ve retention politikası seçilmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Trusted audit-context validation ve appender port | Composition | completed |
| 2 | Allowed ve denied append-only audit | Integration | completed |
| 3 | Audit failure fail-closed | Negative check | completed |

## Tamamlama kanıtı

- Composition audit tenant/actor/company scope'unu execution scope ile birebir doğrular ve empty audit ID'yi reddeder.
- Reporting platform tablosuna doğrudan bağımlı değildir; ortak transaction-bound appender portunu kullanır.
- Allowed sorgu non-sensitive cross-foot target ile, denied sorgu target ID olmadan append-only audit yazdı.
- Zorlanmış appender hatasında authoritative rapor yüklenmiş olsa bile sonuç caller'a dönmedi.
- Uygulama rolüne audit `SELECT` hakkı verilmedi; mevcut schema-owner audit persistence test sınırı korundu.
- Gerçek PostgreSQL integration, solution build, domain/architecture ve format kapıları geçti.
- Production follow-up, bu appender portunu `party.account.detail` version `1` ve `reporting.party-account.view` sözleşmesiyle transaction-owning adapter/endpoint'e bağladı; allowed, denied ve not-found commit kanıtı [production query planında](2026-08-29-party-report-api-query.md) tamamlandı.
