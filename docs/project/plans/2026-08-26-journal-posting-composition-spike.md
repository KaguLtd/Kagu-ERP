# MP-03 Journal Posting Composition Technical Spike

- **Amaç:** Canonical preparation, posted journal, posted audit ve posted outbox fact'lerini tek caller-owned PostgreSQL transaction'ında birleştirmek.
- **Master fazı:** MP-03 / prepare→post composition.
- **Risk:** R4 — posted mali sonuç ile audit/outbox'ın kısmi commit olması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-001`, `ACC-INV-005`, `WFL-INV-002`, `API-005`.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Preparation→posted writer composition | Build | completed |
| 2 | Ayrı posted audit/outbox fact'leri | Integration | completed |
| 3 | Tam commit ve forced rollback | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- `PostgresJournalPostingOrchestrator`, authoritative approval-gated canonical preparation ile immutable posted journal writer'ını aynı caller-owned connection/transaction içinde sıralar.
- Posted journal'dan sonra ayrı `journal-posted` audit ve outbox fact'leri yazılır; outbox yazılamazsa işlem hata verir ve commit yetkisi caller'da kaldığı için bütün fact'ler rollback edilir.
- Gerçek PostgreSQL entegrasyon testi tam commit yolunda posted journal, audit ve outbox'ın birer kez oluştuğunu; zorlanmış outbox hatasında üçünün de sıfır kaldığını doğruladı.
- `scripts/verify.ps1` 26 Ağustos 2026 tarihinde .NET (0 uyarı/0 hata, 58 domain kontrolü), architecture/API, web lint/typecheck/6 test/build, mevcut ve boş PostgreSQL'de 15 migration/RLS/integration, Keycloak, restore ve Android kapılarından geçti.

## Açık sınır

Bu dilim public endpoint veya yasal numaralama açmaz. Üst seviye idempotency completion'ın final posted response'a taşınması, reversal persistence ve gerçek source/workflow command'ları ayrıca kanıtlanacaktır.

## 31 Ağustos 2026 composite approval subject continuation

Reconciliation gibi tek immutable onayın birden fazla tarihli journal kaynağını kapsadığı durumda journal source identity ile workflow subject identity aynı değildir. `ApprovalSubjectReference`, bu iki kimliği yalnız aynı tenant/company kapsamında ayırır. Preparation ve posting orchestrator'ları explicit subject verilmezse eski source-derived davranışı aynen korur; verilirse authoritative completion evidence'ı exact subject/version ile yükler ve posted writer aynı bağı tekrar doğrular. Böylece çok günlük banka satırlarını tek yanlış effective date'e sıkıştırmadan proposal-level maker-checker kanıtı korunabilir.

Accounting Application ve Infrastructure hedef derlemeleri `0 warning / 0 error` geçti. Transit journal fact/factory ve aynı transaction Treasury approval→çoklu journal composition sıradaki açık adımdır; bu altyapı değişikliği tek başına banka GL hesabını kapatmaz.
