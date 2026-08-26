# MP-03 Approval-Gated Journal Preparation Composition Technical Spike

- **Amaç:** Canonical journal preparation'ı exact source version'a bağlı authoritative approval evidence olmadan persistence üretmeyecek biçimde kapılamak.
- **Master fazı:** MP-03 / approval ve journal preparation composition.
- **Risk:** R4 — onaysız veya eski belge sürümü onayıyla journal fact üretme.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `WFL-INV-002`, `WFL-INV-003`, `ACC-INV-005`, `API-005`.
- **Definition of Ready:** Authoritative completed evidence loader hazırdır; gerçek workflow policy/write command ve posted journal kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Canonical source→approval subject binding | Build | completed |
| 2 | Approval-before-persistence transaction order | Integration | completed |
| 3 | Missing/version/replay/rollback negatifleri | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Done when

- Approval subject, canonical source type/event id ve expected source version'dan server-side türetilir.
- Permission ve canonical source/version doğrulamasından sonra, reservation/draft/audit/outbox öncesinde authoritative approval yüklenir.
- Missing/stale evidence hiçbir journal fact veya idempotency completion bırakmaz.
- Aynı idempotency replay'i source veya approval loader'ı yeniden çalıştırmaz.
- Tam repository doğrulaması geçer.

## Tamamlanma kanıtı

- Canonical preparation, approval subject type/id/version değerlerini source identity ve expected source version'dan server-side türetiyor; caller approval snapshot sağlayamıyor.
- Permission, canonical source identity ve version kontrollerinden sonra; period/account/dimension/currency yükleme ve reservation/draft/audit/outbox persistence öncesinde authoritative approval loader aynı transaction'da çalışıyor.
- Gerçek PostgreSQL'de onaylı preparation başarıyla commit oldu; missing approval `APPROVAL_COMPLETION_NOT_FOUND` üretti ve sıfır journal fact bıraktı.
- Idempotent replay canonical source'u yeniden yüklemeden ilk response'u döndürdüğü için approval yolunu da yeniden çalıştırmıyor; rollback ve temiz retry atomik kaldı.
- `scripts/verify.ps1` 26 Ağustos 2026 tarihinde 13 migration'lı mevcut/boş PostgreSQL, restore, RLS, Keycloak auth, .NET, web ve Android kapılarının tamamında geçti.

## Açık sınırlar

Approval kapısı canonical source preparation yolundadır. Düşük seviyeli `PrepareAsync` persistence primitive'i public endpoint değildir ve kontrollü internal test/composition kullanımı içindir. Gerçek source adapter, workflow write/policy authoring ve posted journal/GL persistence hâlâ kapsam dışıdır.
