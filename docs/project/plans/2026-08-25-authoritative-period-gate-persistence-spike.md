# MP-03 Authoritative Period Gate Persistence Technical Spike

- **Amaç:** Journal effective date için PostgreSQL'deki authoritative dönem ve kilit state'ini posting transaction'ında kilitli okuyarak stale caller snapshot'ını ortadan kaldırmak.
- **Master fazı ve kapısı:** MP-03 / backlog 20 teknik posting ön koşulu.
- **Risk sınıfı:** R4 — kapalı döneme kayıt ve eşzamanlı kapanış yarışı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Başlangıç / hedef tarih:** 25 Ağustos 2026 / 25 Ağustos 2026.
- **İlgili requirement ID'leri:** `ACC-INV-004`, `ACC-PER-002`, `ACC-PER-003`, `API-003`.
- **Etkilenen belgeler/modüller:** Accounting migration, Infrastructure period loader, PostgreSQL integration harness.
- **Okunan zorunlu belgeler:** `MASTER_PLAN.md`, `PLANS.md`, Accounting `AGENTS.md`, data architecture, accounting contract ve cross-cutting workflow.
- **Definition of Ready sonucu:** Standart posting için teknik read gate koşullu geçer. Fiscal calendar authoring, close workflow ve reopen policy dahil değildir.

## Kapsam

### Dahil

- Company-scoped accounting period ve current lock-state tabloları.
- Forced RLS; runtime için yalnız `SELECT`.
- Effective date için sıfır veya birden çok dönem eşleşmesinde fail-closed davranış.
- Dönem kimliği için transaction-scoped advisory lock ve kilit alındıktan sonra effective-date eşleşmesinin yeniden doğrulanması.
- Eşzamanlı kapanış protokolünün aynı dönem advisory lock'ını alamadığının gerçek PostgreSQL kanıtı.

### Dahil değil

- Dönem oluşturma/kapatma API'si, approval, maker-checker ve reopen.
- Posted journal state veya mali bakiye.
- Fiscal year uzunluğu, takvim veya soft-close istisna politikası.

## Değişmezler ve güvenlik sınırları

- Runtime rolü dönem veya lock state değiştiremez.
- Birden çok tarih eşleşmesi güvenli varsayımla seçilmez; reddedilir.
- Posting ve close akışları aynı canonical dönem advisory lock anahtarını transaction boyunca taşır.
- Loader transaction'ı commit etmez ve posted durum üretmez.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Forward period/lock migration | Empty/current DB | completed |
| 2 | Transaction-bound authoritative loader | Real DB integration | completed |
| 3 | Concurrency, RLS ve privilege kanıtı | Negative integration | completed |
| 4 | Full repository doğrulaması | Full verify | completed |

## İlerleme günlüğü

- 2026-08-25: Scope ve sınırlar belirlendi; overlap halinde loader'ın fail-closed davranması seçildi. DB authoring workflow sonraya bırakıldı.
- 2026-08-25: `FOR SHARE` runtime role için `UPDATE` privilege gerektirdiğinden reddedildi. Runtime'a mutation yetkisi vermeden canonical transaction advisory lock + yeniden okuma protokolüne geçildi.
- 2026-08-25: Mevcut DB 0007'yi bir kez, boş DB yedi migration'ı sıfırdan uyguladı; ikinci çalıştırmalar sıfır migration verdi. Açık/eksik/çakışan/kapalı dönem, RLS, privilege ve concurrent-close kontrolleri geçti.
- 2026-08-25: Full repository doğrulaması geçti.

## Tamamlanma kanıtı

- [x] Mevcut/boş DB migration ve idempotency geçer.
- [x] Açık dönem yüklenir; eksik/çakışan/kapalı dönem reddedilir.
- [x] Runtime mutation ve cross-company read reddedilir.
- [x] Concurrent close protokolü, posting transaction'ı bitmeden dönem advisory lock'ını alamaz.
- [x] Full verify geçer; commit/push yapılmadı.
