# MP-03 Canonical Source Version Binding Technical Spike

- **Amaç:** Journal preparation komutunu kaynak belgenin beklenen concurrency sürümüne bağlamak.
- **Master fazı:** MP-03 / posting pipeline adım 1 ve API idempotency ön koşulu.
- **Risk:** R4 — retry sırasında değişmiş kaynak belgeden farklı journal üretimi veya eski approval'ın yeniden kullanılması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `API-005`, `WFL-INV-002`, `ACC-INV-005`.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Command ve canonical source version sözleşmesi | Build | completed |
| 2 | Exact version binding ve typed mismatch | Integration | completed |
| 3 | Sıfır persistence negatifi | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- Preparation command pozitif `ExpectedSourceVersion`, canonical source sonucu ise authoritative `SourceVersion` taşır.
- Source adapter sonucu beklenen sürümle birebir eşleşmezse `JOURNAL_SOURCE_VERSION_MISMATCH` üretilir; dönem, kanıt, reservation, draft, audit ve outbox yazımına geçilmez.
- Gerçek PostgreSQL testi stale source version için sıfır journal fact kanıtladı.
- `scripts/verify.ps1` 25 Ağustos 2026 tarihinde .NET, web, mevcut/boş PostgreSQL, restore, RLS, auth ve Android kapılarının tamamında geçti.

## Açık sınırlar

Gerçek kaynak adapter'ı document version'ı kendi authoritative tablosundan optimistic concurrency ile yüklemelidir. Bu sözleşme approval veya posted journal üretmez.
