# MP-03 Posted Journal Persistence Technical Spike

- **Amaç:** Hazırlanmış journal'ı period ve exact-version approval kanıtına bağlı immutable posted header/GL line snapshot'ı olarak aynı transaction'da yazmak.
- **Master fazı:** MP-03 / transaction-bound posted journal persistence.
- **Risk:** R4 — dengesiz, onaysız, yanlış dönemli veya yerinde değiştirilebilir mali sonuç.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-001`, `ACC-INV-002`, `ACC-INV-005`, `WFL-INV-002`.
- **Definition of Ready:** Teknik internal journal identity için geçer; yasal journal numarası/defter sırası ve gerçek workflow policy kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Posted header/line migration | Current/empty DB | completed |
| 2 | Transaction-bound idempotent writer | Build/integration | completed |
| 3 | Balance/source/version/scope/immutability negatifleri | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Done when

- Posted header aynı draft, period ve exact source-version approval'a DB foreign key'leriyle bağlıdır.
- Posted lines draft satırlarından server-side kopyalanır; toplam borç=alacak ve line count DB'de korunur.
- Runtime posted header/line için `UPDATE`/`DELETE` alamaz.
- Aynı draft retry'si ikinci posted journal üretmez; farklı bağlam conflict verir.
- Bu teknik kimlik yasal journal numarası olarak sunulmaz.

## Tamamlanma kanıtı

- `0014_posted_journal` migration'ı immutable header/line tablolarını draft, period ve exact source type/event/version approval composite FK'leriyle ekledi; runtime yalnız `SELECT/INSERT` yetkilidir ve forced RLS aktiftir.
- `0015_posted_journal_balance_guard` deferred constraint trigger'ları commit anında line count, debit ve credit toplamlarını immutable header'a cross-foot ediyor. Owner rolüyle satır tutarı bozma denemesi `23514/ck_posted_journal_cross_foot` ile reddedildi.
- Transaction-bound writer header değerlerini persisted draft/source kayıtlarından, GL satırlarını validated draft satırlarından server-side kopyalıyor; caller mali satır sağlayamıyor.
- Aynı draft retry'si ilk internal journal ID ve DB-normalized `posted_at` değerini döndürdü; ikinci posted fact üretmedi.
- Gerçek PostgreSQL'de dengeli header/satır sayısı, cross-company görünmezlik ve runtime `UPDATE/DELETE` yasağı doğrulandı.
- `scripts/verify.ps1` 26 Ağustos 2026 tarihinde 15 migration'lı mevcut/boş PostgreSQL, restore, RLS, Keycloak auth, .NET, web ve Android kapılarının tamamında geçti.

## Açık sınırlar

Internal `journal_id` yasal yevmiye numarası değildir. Writer henüz canonical preparation, posted audit ve `journal-posted` outbox fact'iyle tek üst seviye composition'a bağlanmadı. Reversal persistence, resmi numaralama/defter sırası, gerçek source adapter ve workflow write/policy authoring sonraki dilimlerdir.
