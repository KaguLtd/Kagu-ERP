# MP-03 Idempotent Journal Preparation Composition Technical Spike

- **Amaç:** Canonical source preparation ile PostgreSQL idempotency acquire/complete akışını tek caller-owned transaction'da birleştirmek.
- **Master fazı:** MP-03 / API-005 ve backlog 20.
- **Risk:** R4 — retry ile ikinci source load, ikinci journal fact veya farklı belge sürümünde response replay.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `API-005`, `ACC-INV-005`.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Canonical source+version request hash | Unit/build | completed |
| 2 | Acquire→prepare→complete transaction composition | Integration | completed |
| 3 | Replay/no-reload, changed version conflict ve rollback | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- Request hash tenant/company/source type/source event/posting purpose/expected source version alanlarının invariant canonical biçiminden SHA-256 ile üretilir; server-generated teknik fact kimlikleri hash'e girmez.
- Accounting Infrastructure composition idempotency acquire/complete adaptörlerini port olarak alır; Bootstrap'a ters modül bağımlılığı eklemez.
- İlk çağrı idempotency kaydı, canonical source preparation, reservation, draft, audit, outbox ve `201` response snapshot'ını aynı caller-owned transaction'da tamamlar.
- Aynı key/payload replay'i source loader'ı çağırmadan ilk response'u döndürür ve ikinci journal fact üretmez. Aynı key ile farklı expected source version `IDEMPOTENCY_KEY_REUSED` üretir.
- Caller rollback idempotency kaydı dahil bütün fact'leri geri alır; aynı key ile sonraki deneme replay değil yeni başarılı işlem olarak tamamlanır.
- `scripts/verify.ps1` 25 Ağustos 2026 tarihinde .NET, web, mevcut/boş PostgreSQL, restore, RLS, auth ve Android kapılarının tamamında geçti.

## Açık sınırlar

Composition public HTTP endpoint, gerçek source adapter, approval veya posted journal/GL sonucu değildir. Response snapshot PII içermeyen use-case DTO ile sınırlı tutulmalıdır.
