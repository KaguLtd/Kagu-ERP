# 04 — Veri Mimarisi ve Bütünlük Standardı

- **Sahip:** Teknik mimar + muhasebe sahibi
- **Uygulama:** Tüm iş modülleri
- **İlke:** İş kuralı yalnız uygulama koduna bırakılmaz; mümkün olan bütünlük DB constraint, unique index ve transaction ile de korunur.

## 1. PostgreSQL yerleşimi

### Ayrı database ve roller

| Database | Sahip | Uygulama rolü | Amaç |
|---|---|---|---|
| `erp` | `erp_owner` | `erp_app`, `erp_migrator`, `erp_readonly`, `erp_backup` | ERP iş verisi |
| `keycloak` | `keycloak_owner` | `keycloak_app`, `keycloak_backup` | Kimlik state'i |

- `erp_app` schema/table sahibi olamaz, DDL yapamaz ve `BYPASSRLS` alamaz.
- `erp_migrator` yalnız kontrollü migration job'ında kullanılır.
- `erp_readonly` denetim/BI için view bazlı ve scope kontrollüdür.
- `erp_backup` eksiksiz physical backup için ayrı, host düzeyinde denetlenmiş hesaptır.
- Public schema create yetkisi kaldırılır; `search_path` sabitlenir ve kullanıcı girdisi içermez.

## 2. Şema sahipliği

| Schema | Owner modül | Örnek tablolar |
|---|---|---|
| `iam` | Identity | `user_profile`, `role`, `permission`, `user_scope` |
| `org` | Organization | `tenant`, `company`, `branch`, `warehouse`, `fiscal_period` |
| `party` | Parties | `party`, `party_account`, `open_item`, `settlement` |
| `inventory` | Inventory | `item`, `stock_movement`, `reservation`, `cost_layer` |
| `sales` | Sales | `quote`, `sales_order`, `dispatch`, `sales_invoice` |
| `purchasing` | Purchasing | `purchase_request`, `purchase_order`, `goods_receipt` |
| `treasury` | Treasury | `bank_account`, `payment`, `statement_import`, `bank_transaction` |
| `instruments` | Instruments | `negotiable_instrument`, `instrument_event`, `endorsement` |
| `accounting` | Accounting | `account`, `journal_entry`, `journal_line`, `period_lock` |
| `compliance` | Compliance | `tax_rule`, `tax_decision`, `e_invoice_envelope`, `document_sequence` |
| `workflow` | Workflow | `approval_policy`, `approval_instance`, `approval_task` |
| `platform` | Platform | `attachment`, `audit_event`, `outbox_message`, `idempotency_record` |
| `reporting` | Reporting | Read model/materialized view metadata |

Modül kendi şemasına yazma sahibidir. Cross-module foreign key yalnız döngü yaratmıyor ve yaşam döngüsü açıkça aynı transaction'daysa kullanılır; aksi halde immutable ID + application contract doğrulaması.

## 3. Ortak kolon standardı

Değişebilir master tablo:

```text
id uuid primary key
tenant_id uuid not null
company_id uuid null/not null by domain
code varchar(...) not null
version bigint not null default 1
created_at timestamptz not null
created_by uuid not null
updated_at timestamptz not null
updated_by uuid not null
is_active boolean not null default true
```

Append-only hareket tablo:

```text
id uuid primary key
tenant_id uuid not null
company_id uuid not null
source_type varchar not null
source_id uuid not null
source_line_id uuid null
occurred_at timestamptz not null
legal_date date not null
created_at timestamptz not null
created_by uuid not null
reversal_of_id uuid null
```

Hareket tablosunda `updated_*` ve soft delete yoktur.

## 4. Kimlik ve iş anahtarı

- Internal primary key: UUIDv7.
- Kullanıcının gördüğü kod/numara ayrı natural key'dir.
- API resource ID UUID olarak taşınır; tahmin edilebilir seri numarası authorization yerine geçmez.
- Dış sistem kimliği `(provider, external_type, external_id)` unique olmalıdır.
- Import tekrarında `source_system + source_record_id + source_version` dedup anahtarıdır.

## 5. Para, miktar ve kur

| Alan | DB tipi | C# tipi | JSON |
|---|---|---|---|
| Para tutarı | `numeric(20,4)` | `decimal` | string |
| Miktar | `numeric(20,6)` | `decimal` | string |
| Kur | `numeric(20,10)` | `decimal` | string |
| Yüzde/oran | `numeric(12,8)` | `decimal` | string |

- Her tutar yanında para birimi veya açıkça aynı başlık bağlamı taşır.
- Belge, vergi ve GL satırları işlem ve fonksiyonel tutarlarını saklar.
- Kur `rate_type`, `source`, `rate_date`, `numerator`, `denominator`, `approved_at/by` ile snapshot edilir.
- Yuvarlama `MidpointRounding`/scale gibi açık politika kimliğine bağlıdır.
- Toplam, satırların saklanan yuvarlanmış sonuçları üzerinden yeniden üretilebilir.
- `NaN`, infinity veya binary float DB/API'ye giremez.

## 6. Tarih ve zaman

- Sistem zamanı UTC ve NTP senkronize.
- Olay timestamp'i `timestamptz` UTC.
- Yasal fatura/işlem tarihi `date`; gerekirse yerel saat/dakika ayrıca.
- Uygulama yasal timezone'u `Europe/Nicosia` olarak yapılandırır; server OS timezone'una güvenmez.
- Etki tarihli kurallar `[effective_from, effective_to)` yarı açık aralığı kullanır.
- Backdating için açık permission, açık dönem ve gerekçe gerekir.

## 7. Tenant/company RLS

Korunan her tablo için:

```sql
ALTER TABLE ... ENABLE ROW LEVEL SECURITY;
ALTER TABLE ... FORCE ROW LEVEL SECURITY;
```

Politika yaklaşımı:

- `tenant_id = current_setting('app.tenant_id')::uuid` zorunlu.
- Company-scoped tabloda `company_id = ANY(current_setting('app.company_ids')::uuid[])` benzeri kontrollü fonksiyon.
- `WITH CHECK` INSERT/UPDATE için aynı scope'u zorunlu kılar.
- Request/job transaction başında `SET LOCAL`; connection pool'a context sızmadığı test edilir.
- RLS policy fonksiyonu `SECURITY DEFINER` ise sabit `search_path`, minimal owner ve ayrı review gerekir.
- Table owner ve superuser üzerinden uygulama testi yapılmaz.

### Zorunlu negatif testler

1. Tenant A tokenıyla Tenant B UUID'si.
2. Aynı tenant içinde yetkisiz Company B.
3. Yetkisiz şube/depo/banka hesabı.
4. Background job eksik context.
5. Reused pooled connection üzerinde önceki tenant kalıntısı.
6. Export/report/attachment indirme scope'u.

## 8. Eşzamanlılık

- Değişebilir aggregate `version bigint`; update `WHERE id=@id AND version=@expected`.
- API ETag bu sürümden türetilir; mismatch `409` veya `412` ve güncel özet döner.
- Belge numarası satırı `SELECT ... FOR UPDATE` veya transaction-scoped advisory lock.
- Stok rezervasyonu tek SQL/transaction ile available kontrolü ve artırımı yapar.
- Settlement açık kalan üzerinde lock + constraint ile.
- Long-running UI edit lock kullanılmaz; optimistic conflict ekranı gösterilir.

## 9. Değişmezlik

Posted tablolarda uygulama katmanı yasağına ek olarak:

- Update/delete permission DB rolünde kaldırılır veya trigger yalnız izinli sistem kolonları dışında engeller.
- Reversal ilişkisi `reversal_of_id`; bir hareket birden fazla aktif reversal alamaz.
- Audit trigger business audit'in yerine geçmez; doğrudan DB değişikliğini yakalayan ek kontroldür.
- Posted belgeye ek açıklama gerekiyorsa append-only note/amendment kaydı eklenir.

## 10. Muhasebe constraintleri

- `journal_entry` ve satırlar tek transaction'da.
- Para ölçeğine göre borç/alacak non-negative; aynı satırda ikisi birden pozitif olamaz.
- Entry posting öncesi toplam borç=alacak; DB deferred constraint trigger veya atomik stored validation ile korunur.
- Aynı `source_type/source_id/posting_purpose` için tek aktif journal unique index.
- Kapalı dönem kontrolü posting command ve DB fonksiyonu içinde.
- Manual journal kaynak türü, gerekçe, ek ve çift onay ister.

## 11. Stok constraintleri

- Her movement signed quantity ve açık movement type taşır.
- Transfer iki hareket + tek transfer kimliği; source negatif, destination pozitif ve toplam sıfır.
- Seri takipli üründe miktar/seri cardinality eşit.
- Lot/seri başka item/company ile eşleşemez.
- Negatif stok varsayılan blok; istisna policy snapshot ve audit.
- Cost layer toplamı movement değerine mutabık.

## 12. Belge sırası

`compliance.document_sequence` anahtarı:

`tenant_id + company_id + branch_id + fiscal_year + document_type + series`

- Numara transaction içinde ayrılır.
- Ayrılan numara yeniden kullanılmaz.
- Void/gap nedeni ayrı `sequence_event` kaydında.
- e-Fatura formatı modül belgesindeki resmi yapıya göre üretilir.
- Idempotency kaydı aynı request'e aynı belge/numarayı döndürür.

## 13. İndeksleme

- Her FK üzerinde uygun index.
- Liste sorguları için scope kolonları index'in başında: `(tenant_id, company_id, ...)`.
- Açık kalem için partial index `remaining_amount <> 0`.
- Outbox için `processed_at IS NULL` partial index ve `next_attempt_at`.
- Banka transaction dedup için provider transaction ID/file hash unique.
- Free text için kontrollü `pg_trgm`/FTS; VKN/IBAN exact normalized index.
- Index ekleme gerçek `EXPLAIN (ANALYZE, BUFFERS)` kanıtına dayanır; gereksiz index write maliyetidir.

## 14. Bölümleme ve arşivleme

İlk günden bölümleme zorunlu değildir. Aşağıdaki tablolar 20–50 milyon satıra veya bakım sorununa ulaştığında tarih + tenant stratejisiyle değerlendirilebilir:

- `audit_event`
- `journal_line`
- `stock_movement`
- `outbox_message`
- `integration_log`

Partition kararı sorgu, retention, vacuum ve backup ölçümlerine dayanır. Eski partition drop etmek yasal saklama onayı olmadan yapılamaz.

## 15. Migration standardı

1. **Expand:** Yeni nullable kolon/tablo/index; eski kod çalışmaya devam eder.
2. **Migrate:** Backfill küçük batch, throttling, progress ve restartability.
3. **Switch:** Yeni kod dual-read/write gerekirse kontrollü flag.
4. **Validate:** Count, checksum, finansal mutabakat ve constraint validate.
5. **Contract:** Eski kolon/API sonraki release ve onayla kaldırılır.

- Production migration app startup'ında çalışmaz.
- Büyük index `CONCURRENTLY` ve transaction sınırlaması dikkate alınır.
- Destructive migration önce yedek/restore point ve dry-run ister.
- Migration geri dönüşü veri kaybı yaratacaksa rollback değil ileri compensation planı yazılır.

## 16. Seed ve referans veri

- `reference`: ISO para, ülke, birim, resmi hesap planı, kod listesi; kaynak/checksum/sürüm.
- `demo`: Sentetik örnek şirket; production'a kurulmaz.
- `test`: Her test kendi minimal fixture'ını kurar.
- Hukuki referans veri admin UI'da doğrudan edit edilmez; taslak → onay → publish akışı.

## 17. Retention ve silme

- Retention class her veri/dosyaya atanır.
- Finansal/yasal kayıt süre dolmadan silinmez; legal hold silmeyi durdurur.
- Kullanıcı/personel profili ile yasal işlem kaydı ayrılır; gereksiz PII anonimleştirilebilirken işlem kanıtı korunur.
- Hard delete job'u dry-run raporu, DPO/muhasebe onayı, idempotency ve audit ile.
- Backup içindeki silinmiş verinin yaşam döngüsü ayrıca belgelenir.

## 18. Veri kalite ölçümleri

- Zorunlu alan eksikliği, duplicate natural key, orphan FK.
- Cari/GL, stok/GL, banka/GL ve çek/GL mutabakat farkı.
- Unknown currency/rate, invalid VKN/IBAN, sequence gap.
- Gelecek/kapalı dönem tarihleri.
- Outbox yaşı ve idempotency çatışması.

Bu kontroller release ve gece job'ında dashboard'a ölçüm üretir.

## 19. Kanonik olay–defter veri modeli

Araştırma sonrası şu kayıtlar birbirinin yerine kullanılamaz:

| Kayıt | Asgari anahtar/bağ | Değişmezlik |
|---|---|---|
| EconomicEvent | source document/line, type, effective date, internal/external agent | posted sonrası append-only |
| SubledgerEntry | economic event, ledger type, account/resource, signed amount/quantity | append-only |
| JournalEntry/Line | economic event, posting purpose, rule snapshot, dimensions | booked sonrası append-only |
| DueScheduleLine/OpenItem | source receivable/payable, due date, original amount | kaynak tutar değişmez; kalan türetilir |
| Payment | treasury source, amount, currency, instrument, effective date | posted sonrası append-only |
| PaymentAllocation | payment/credit, open item, allocated amount, allocation event | allocation/unallocation olayları append-only |
| StatementLine | statement/file hash, bank reference, booking/value date | normalize edilmiş alan + değişmez raw referans |
| ReconciliationMatch | statement line, internal payment/entry, amount, decision | öneri değişebilir; approved karar karşı olayla düzeltilir |
| ProjectionGeneration | projection type, reason, source range, rule version, checksum | her rebuild için yeni kayıt |

OpenItem.remaining_amount ve hesap/ürün bakiyesi fiziksel otorite kolon değildir. Bunlar ledger + allocation üzerinden hesaplanır; performans için snapshot tutulursa generation, as_of ve reconciliation_status taşır.

## 20. Kaynak satır bağlantıları ve party rolleri

- Tek Party kaydı müşteri, tedarikçi, çalışan, banka veya diğer rollerden birden çoğunu tarih etkili PartyRole ile taşıyabilir.
- PartyAccount şirket, rol, para ve GL control account bağlamındadır; aynı tüzel kişiyi rol başına kopyalamak yasaktır.
- Sipariş, sevk/teslim, kabul ve fatura satırları bire bir varsayılmaz. SourceLineLink; source/target type-id-line, linked quantity, amount, UOM dönüşümü ve reversal bağını taşır.
- Bir sipariş satırı birden çok sevke; bir fatura satırı birden çok sevk/kabul satırına bağlanabilir. Kalan miktar link hareketlerinden türetilir.
- InventoryBalance bağlamı item + owner + custodian/facility + location + lot/serial + status’tür. Fiziksel konum ekonomik sahiplik anlamına gelmez.

## 21. Bitemporal ve deterministik posting

- effective_date iş etkisini; recorded_at sistem bilgisinin ne zaman geldiğini gösterir. İkisi değiştirilemez.
- posted_at, posting_generation ve posting_rule_version her türetilmiş mali satırda izlenir.
- Aynı effective_date için sequence_key deterministiktir; geçmiş tarihli stok değerleme bu sıradan sonraki bağımlı cost layer’ları etkiler.
- Backdate komutu önce etki analizi üretir: açık/kapalı dönem, vergi kilidi, stok değerleme zinciri, rapor snapshot ve dış beyan etkisi.
- Kapalı dönemi aşan düzeltme, sessizce eski döneme yazılmaz; yetkili muhasebe politikası correction period ve disclosure kaydı belirler.

## 22. Repost ve projection rebuild güvenliği

1. Kaynak veri checksum’u, kapsanan ID/tarih aralığı ve mevcut generation dondurulur.
2. Yetkili kullanıcı neden, ticket ve beklenen farkı girer; kritik kapsam çift onay ister.
3. Yeni projection staging alanda üretilir; borç=alacak, satır sayısı, para/boyut ve control-account mutabakatı yapılır.
4. Fark yalnız açıklanmış kural/bug düzeltmesine uyuyorsa atomik generation switch yapılır.
5. Eski generation erişilebilir audit kanıtıdır; business event veya dış mesaj tekrar gönderilmez.
6. İşlem sonucu önce/sonra hash, fark raporu, süre ve aktörle saklanır.

Repost; fatura, ödeme, stok hareketi veya yasal belgeyi düzeltme aracı değildir. Bu kayıtlar reversal, return, credit/debit note veya yeni adjustment olayı ister.

## 23. Ek veri kalite ve DB kontrolleri

- Her control account için subledger total = GL total; şirket, para, effective-date kesimi ve dimension aynı olmalıdır.
- Payment allocation toplamı payment usable amount’ı; open item allocation toplamı original amount’ı aşamaz.
- Statement açılış + girişler − çıkışlar = kapanış; dosya sayım/tutar kontrol toplamları saklanır.
- Projection satırı geçerli kaynak olay ve generation olmadan var olamaz.
- Strict legal sequence ve gap-tolerant internal sequence ayrı tiplerdir; numara boşluğu nedeni olaydır.
- Decimal para/miktar API’den string gelir; DB scale ve currency minor-unit politikasıyla doğrulanır.
