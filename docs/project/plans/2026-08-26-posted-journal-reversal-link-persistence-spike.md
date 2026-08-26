# MP-03 Posted Journal Reversal Link Persistence Technical Spike

- **Amaç:** İki immutable posted journal arasında tekil ve tam ters satır kanıtlı reversal bağını PostgreSQL'de kurmak.
- **Master fazı:** MP-03 / reversal persistence.
- **Risk:** R4 — orijinal kaydın değiştirilmesi, ikinci reversal veya eksik karşı etkinin kabulü.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-003`.

## Sınır

Bu dilim reversal effective date/correction period seçmez, kapalı dönem açmaz, permission/approval politikası veya public command tanımlamaz. Yalnız zaten-posted iki fişin scope, tekillik ve tam debit/credit karşılığını kalıcılaştırır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Append-only reversal link migration | Migration | completed |
| 2 | Exact opposite DB guard | Real PostgreSQL | completed |
| 3 | Concurrency, RLS ve privilege testleri | Integration | completed |
| 4 | Repository doğrulama ve docs | Gate | completed |

## Tamamlanma kanıtı

- `0016` migration'ı scope-bound original/reversal FK'leri, original başına tek reversal, farklı journal kimliği, forced RLS ve runtime `SELECT/INSERT` sınırını kurdu. `0017`, currency snapshot metadata'sının aynen korunmasını ve transaction/functional debit-credit taraflarının tersliğini DB guard'a ekledi.
- Transaction-bound writer ilk bağı oluşturuyor, aynı çifti immutable timestamp ile replay ediyor ve farklı ikinci reversal için `POSTED_JOURNAL_ALREADY_REVERSED` typed conflict üretiyor.
- Gerçek PostgreSQL testleri tam ters fişi kabul etti; ters olmayan debit/credit'i reddetti; cross-company görünmezliği ve `UPDATE/DELETE` yasağını doğruladı.
- İki ayrı connection/transaction yarışında ikinci contender unique lock üzerinde ilk commit'i bekledi; yalnız ilk reversal kazandı ve kaybeden kazanan reversal kimliğiyle conflict aldı.
- İzole Kagu ERP PostgreSQL kümesinde 17 migration uygulandı. Application Control bağımsız integration DLL'ini `0x800711C7` ile engellediğinde aynı linked source test paketi repository'nin architecture host `database` modu üzerinden güvenlik politikası değiştirilmeden geçti.
- Release build 0 uyarı/0 hata, 58 domain kontrolü, API/architecture, `dotnet format` ve `git diff --check` geçti.

## Açık sınır

Reversal effective date/correction period, kapalı dönem disclosure, reversal permission/approval, audit/outbox/idempotency composition ve public command hâlâ owner/policy kararı gerektirir.
