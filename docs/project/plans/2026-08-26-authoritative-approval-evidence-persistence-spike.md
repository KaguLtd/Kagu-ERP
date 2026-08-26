# MP-03 Authoritative Approval Evidence Persistence Technical Spike

- **Amaç:** Completed approval evidence'ı exact subject version ve company scope ile PostgreSQL'den fail-closed yüklemek.
- **Master fazı:** MP-03 / approval ve posted persistence ön koşulu.
- **Risk:** R4 — eski sürüm onayını kullanma, cross-company kanıt sızıntısı veya runtime kanıt değiştirme.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `WFL-INV-002`, `WFL-INV-003`, `WFL-INV-005`.
- **Definition of Ready:** Immutable completed evidence persistence için geçer; workflow policy authoring, eligible approver seçimi ve posted journal kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Completed instance/decision migration | Current/empty DB | completed |
| 2 | Transaction-bound authoritative loader | Build/integration | completed |
| 3 | Missing/version/scope/read-only negatifleri | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Done when

- Runtime rolü workflow evidence tablolarında yalnız `SELECT` yetkilidir ve forced RLS aktiftir.
- Loader exact tenant/company/subject type/id/version için tek completed snapshot yükler.
- Kararlar domain modelindeki distinct-person quorum ve maker-checker invariantlarından yeniden geçirilir.
- Eksik, eski sürümlü ve cross-company evidence fail-closed reddedilir.
- Mevcut/boş DB, restore, auth, web ve Android dahil repository kapıları geçer.

## Tamamlanma kanıtı

- `0013_approval_completion_evidence` migration'ı workflow-owned completion/decision snapshot tablolarını exact subject version uniqueness, distinct approver constraint, forced RLS ve runtime `SELECT` sınırıyla ekledi.
- Transaction-bound loader exact tenant/company/subject type/id/version için kanıtı yükleyip kararları `ApprovalCompletionEvidence` invariantlarından yeniden geçiriyor.
- Gerçek PostgreSQL testleri geçerli kanıt, eski/eksik subject version, cross-company görünmezlik ve read-only runtime privilege senaryolarında geçti.
- Integration fixture identity'si tenant'a özgü hale getirildi; başarısız koşudan kalan test kaydı sonraki koşuyu çakıştırmıyor ve güvenli cleanup kendi tenant'ını FK sırasıyla temizliyor.
- `scripts/verify.ps1` 26 Ağustos 2026 tarihinde 13 migration'lı mevcut/boş PostgreSQL, restore, RLS, Keycloak auth, .NET, web ve Android kapılarının tamamında geçti.

## Açık sınırlar

Bu dilim workflow policy yayımlamaz, eligible approver seti veya quorum seçmez ve approval command/write workflow'u açmaz. Posting orchestration'ın loader'ı aynı transaction'a dahil etmesi ve posted journal/GL persistence sonraki MP-03 dilimleridir.
