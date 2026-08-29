# MP-03 Atomic Party Report Projection Publication Technical Spike

- **Amaç:** Manifest, policy, statement, aging ve control-account snapshot'larını tek caller-owned transaction'da ön doğrulamalı yayımlamak.
- **Master fazı:** MP-03 / backlog 18.
- **Risk:** R4 — kısmi/orphan generation veya cross-foot etmeyen projection seti.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `RPT-INV-001`, `RPT-INV-003`, `RPT-INV-005`, `RPT-INV-006`, `RPT-PARTY-002`, `RPT-CTRL-001`, `RPT-CTRL-002`.

## Sınır

Composition transaction açmaz/commit etmez ve source query/job scheduling politikası seçmez. Yalnız caller'ın sunduğu doğrulanmış snapshot setini atomik writer sırasına bağlar.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Pre-write statement-aging ve subledger-GL cross-foot | Composition | completed |
| 2 | Manifest→policy→projection deterministic publication | Integration | completed |
| 3 | Exact replay ve invalid-set zero-write | Negative check | completed |

## Tamamlama kanıtı

- Publisher bütün component'ların report slice alanlarını, statement-aging cross-foot'u ve subledger-GL reconciliation'ı herhangi bir write öncesinde doğrular.
- Writer sırası manifest → policy → statement → aging → subledger → GL olarak tek caller-owned transaction içinde sabittir.
- Tam immutable set replay'i hiçbir yeni fact üretmedi.
- Farklı generation taşıyan GL snapshot'ı typed publication mismatch ile bütün writer çağrılarından önce reddedildi; foreign generation için satır sayısı sıfır kaldı.
- Composition transaction commit etmez ve source query/job policy seçmez.
- Gerçek PostgreSQL integration, solution build, domain/architecture ve format kapıları geçti.
