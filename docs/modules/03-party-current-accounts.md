# PARTY — Cari Kart, Açık Kalem ve Mutabakat

## 1. Amaç

Müşteri, tedarikçi ve diğer iş ortaklarının kimliği, adres/banka bilgileri, şirket bazlı cari hesabı, borç/alacak hareketleri, açık kalem kapamaları, vade, kredi limiti, risk ve ekstreyi yönetir.

## 2. Model

- `Party`: Gerçek/tüzel kişi, ad/unvan, normalized arama adı, kimlik tipi.
- `PartyRole`: Customer, supplier, employee, other; tarih etkili.
- `PartyTaxIdentity`: Sicil/VKN ve doğrulama kaynağı.
- `PartyAddress`: Fatura/sevk/yasal, tarih etkili.
- `PartyContact`: Telefon/e-posta, tercih ve kişisel veri sınıfı.
- `PartyBankAccount`: IBAN/hesap, banka, currency, doğrulama ve approval state.
- `PartyAccount`: Company + party + receivable/payable role + currency + control account/settlement policy.
- `ArApEntry`: Append-only borç/alacak hareketi ve source.
- `OpenItem`: Fatura/borç/alacak kaynaklı açık kalem.
- `Settlement`: Ödeme/kredi/mahsup ile open item bağlantısı.
- `CreditLimit` ve `RiskSnapshot`: Tarih etkili limit/policy sonucu.
- `ReconciliationRequest`: Dönem bakiyesi ve taraf yanıtı.

## 3. Cari kimlik ve duplicate önleme

- Normalized unvan + sicil/VKN + telefon/e-posta fuzzy aday üretir; otomatik merge etmez.
- VKN/sicil company/jurisdiction kuralına göre exact unique veya kontrollü exception.
- Party merge yalnız yetkili, preview ve reference remap planıyla; posted kaynak değişmez, alias/redirect korunur.
- Tüzel/gerçek kişi alanları ayrı validation.
- Inactive party yeni belgeye seçilemez; geçmiş görüntülenir.

## 4. Banka hesabı güvenliği

Yeni/değişen tedarikçi IBAN:

`draft → independently_verified → approved → active`.

- Hazırlayan ve onaylayan farklı.
- Eski hesap silinmez; end-date.
- Ödeme dosyası, payment approval anındaki account version/hash'ini taşır.
- IBAN değişikliği açık payment proposal'ları otomatik bloklar ve yeniden onay ister.
- UI varsayılan maskeli; tam görüntü permission + audit.

## 5. Cari hareket

| Kaynak | Tipik etki |
|---|---|
| Satış faturası | Müşteri borcu/open item |
| Satış iade/kredi | Müşteri alacağı veya açık kalem azaltma |
| Alış faturası | Tedarikçi alacağı/open item |
| Ödeme/tahsilat | Karşı cari hareket + settlement |
| Çek/senet | Politika bazlı settlement/risk değişimi |
| Kur değerleme | Fonksiyonel tutar farkı; original currency bakiye değişmez |
| Manual AR/AP adjustment | Ayrı permission, reason, approval ve GL |

Bakiye mutable tek kolon değildir; hareketlerden türetilir. Performans özeti rebuild edilebilir projeksiyondur.

## 6. Çoklu döviz

- Open item original amount/currency, functional amount/rate snapshot.
- Settlement farklı currency ise kullanılan çapraz kur ve realized FX farkı.
- Kalan original ve functional değer açıkça tutulur; rounding residual izinli eşikle otomatik ayrı satır.
- Dönem sonu unrealized valuation yeni adjustment entry; sonraki dönem reversal politikası.

## 7. Settlement invariantları

- `PARTY-INV-001`: Settlement/allocation amount > 0.
- `PARTY-INV-002`: Allocation aynı tenant, company ve party account kapsamındadır.
- `PARTY-INV-003`: Toplam allocation payment available amount ve open item remaining amount'ı aşamaz.
- `PARTY-INV-004`: Posted allocation değişmez; unallocation ayrı reversal event ve GL etkisi üretir.
- `PARTY-INV-005`: Farklı para birimindeki allocation kullanılan kur, functional amount ve rounding snapshot'ını taşır.
- Kısmi kapama ve bir ödemenin çok faturaya dağıtımı desteklenir.
- Otomatik kapama kuralı açıkça seçilir: exact reference, oldest due, kullanıcı dağıtımı.

## 8. Yaşlandırma ve vade

- Vade, belge payment schedule satırlarından.
- Aging as-of date ile yeniden üretilebilir.
- Bucket tenant ayarı: 0–30, 31–60, 61–90, 90+ varsayılan.
- Future-due ayrı; gecikme günü yerel iş takvimi veya takvim günü policy.
- Disputed/blocked kalem bakiye içinde fakat tahsilat planında ayrı gösterilir.

## 9. Kredi ve risk

Önerilen formül sürümlüdür:

```text
risk = açık faturalar
     + sevk edilmiş fakat faturalanmamış
     + onaylı sipariş rezervasyonu
     + policy gereği açık çek riski
     - geçerli teminat değeri
```

- Soft limit uyarı/onay; hard limit blok.
- Limit currency ve functional eşdeğer.
- Override permission, süre, tutar, reason ve approver.
- Risk snapshot açıklanabilir bileşen listesi verir.

## 10. API

```text
GET/POST /api/v1/parties
GET/PATCH /api/v1/parties/{id}
POST /api/v1/parties/{id}/bank-accounts
POST /api/v1/party-bank-accounts/{id}/approve
GET  /api/v1/parties/{id}/statement
GET  /api/v1/parties/{id}/open-items
POST /api/v1/settlements
POST /api/v1/settlements/{id}/reverse
GET  /api/v1/parties/{id}/risk
POST /api/v1/reconciliation-requests
```

## 11. UI

- Cari 360: kimlik, bakiye para kırılımı, açık kalem, risk, sipariş/sevk, çek, banka ve audit sekmeleri.
- Ekstre filtreleri as-of date/currency/branch; her satır kaynak belgeye link.
- Settlement workspace ödeme ve fatura kalanını canlı ama server hesaplı gösterir.
- Hassas alanlar role göre maskeli; clipboard/export ayrıca yetkili.

## 12. Muhasebe ve olaylar

Yayımlanan contract/eventler:

- `PartyCreated`, `PartyBankAccountChanged/Approved`.
- `OpenItemCreated`, `SettlementPosted`, `SettlementReversed`.
- `CreditLimitExceeded`.
- `PartyAccountPostingInstruction` GL hesap mapping'i ile.

PARTY, satış/alış faturası içeriğini değiştirmez; yalnız yayımlanmış source snapshot kullanır.

## 13. Raporlar

- Cari bakiye ve para kırılımı.
- Açık kalem ve yaşlandırma.
- Tahsilat/ödeme performansı.
- Risk/limit aşımı ve override.
- Mutabakat yanıt/fark.
- Değişen/henüz onaysız banka hesapları.

## 14. Kabul testleri

- [ ] 100 GBP fatura + 60 GBP tahsilat = 40 GBP kalan; functional FX farkı ayrı.
- [ ] Paralel settlement toplamı açık kalemi aşmıyor.
- [ ] Posted settlement update/delete reddediliyor; reversal izli.
- [ ] IBAN değişince açık ödeme yeniden onay istiyor.
- [ ] As-of aging geçmiş tarihte doğru yeniden üretiliyor.
- [ ] Cari alt defter toplamı ilgili GL kontrol hesabıyla mutabık.
- [ ] Başka company party account/statement erişimi engelli.

## 15. Party-role ve vade modeli

Tek Party, müşteri ve tedarikçi rollerini birlikte taşıyabilir. PartyRole şirket, geçerlilik aralığı, risk/ödeme politikası ve durum taşır; aynı vergi/kimlik sahibi rol başına kopyalanmaz. Duplicate birleştirme doğrudan silme değil, onaylı canonical-party yönlendirmesi ve eski kimlik auditidir.

Bir fatura tek OpenItem olmak zorunda değildir. DueScheduleLine her taksit için:

- original amount/currency;
- due date ve payment-term snapshot;
- receivable/payable control account;
- allocated, disputed, written-off ve remaining tutarlarını

ayrı izler. Yaşlandırma belge tarihine değil açık policy’ye göre vade tarihine; as-of raporu effective-date + o tarihe kadar kaydedilmiş allocation olaylarına dayanır.

- `PARTY-DUE-001`: Her vade satırı tenant, company, party account, kaynak olay, para, payment-term snapshot, control account, pozitif original amount ve açık due date taşır.
- `PARTY-DUE-002`: Bir kaynağın vade satırları mükerrer olamaz ve original amount toplamı kaynak original amount'a tam eşit olmalıdır.
- `PARTY-OI-001`: Open-item remaining amount mutable otorite değildir; original amount eksi as-of allocation ve write-off karşılıklarından türetilir.
- `PARTY-OI-002`: Unallocation ve write-off reversal, asıl append-only olaya aynı kapsam/para/tutarla bağlanır; asıl olay değiştirilmez veya silinmez.

Persistence uygulama kanıtı (26 Ağustos 2026): Minimal party identity kabuğu, company/currency scoped party account ve immutable due-schedule header/line modeli [teknik spike](../project/plans/2026-08-26-party-account-due-schedule-persistence-spike.md) kapsamında PostgreSQL'e eklendi. Deferred DB guard taksit sayısı ve toplamını kaynağa tam cross-foot eder; aynı source/version tekrarı yalnız bütün header ve taksit snapshot alanları eşleşirse ilk sonucu döndürür. [Authoritative loader](../project/plans/2026-08-26-authoritative-due-schedule-loader-spike.md) aynı transaction ve company scope içinde snapshot'ı domain invariantlarından yeniden geçirir; eksik/bozuk içerik fail-closed olur. Forced RLS, cross-company negatif test ve runtime UPDATE/DELETE reddi gerçek PostgreSQL'de geçti. Remaining balance, payment-term üretimi ve allocation bu persistence diliminin kapsamı değildir.

## 16. Payment allocation defteri

Payment ekonomik nakit olayıdır; PaymentAllocation ödeme/kredi/mahsup ile DueScheduleLine arasındaki bağdır. Allocation:

- ödeme para birimi, açık kalem para birimi, fonksiyonel tutar ve kullanılan kuru snapshot eder;
- payment usable amount ve open item remaining amount sınırını aşamaz;
- bir ödeme çok açık kaleme, bir açık kalem çok ödemeye bağlanabilir;
- unallocation ile karşılanır; kaynak payment veya GL satırı silinmez;
- avans/fazla ödeme için unapplied credit olarak açık kalabilir;
- write-off, iskonto ve kur farkını ayrı reason/account/rule ile üretir.

Settlement terimi kullanıcı yüzünde üst kavram olabilir; veri modelinde allocation, netting, write-off ve bank reconciliation ayrı event type’tır.

Open-item impact persistence kanıtı (26 Ağustos 2026): Mutable remaining alanı oluşturmayan append-only allocation/unallocation/write-off impact defteri [teknik spike](../project/plans/2026-08-26-open-item-impact-persistence-spike.md) kapsamında PostgreSQL'e eklendi. Counter-event trigger'ı original türü, party account, due line, payment, currency ve amount eşitliğini zorlar; aynı event retry'ı yalnız immutable içerik tamamen eşleşirse kabul edilir. Bu teknik defter Treasury payment otoritesi, allocation/write-off onayı, FX veya GL posting politikası değildir.

Authoritative open-item snapshot kanıtı (26 Ağustos 2026): Persisted due-line ve bütün immutable impact geçmişini aynı transaction/company scope içinde yükleyen [as-of loader](../project/plans/2026-08-26-authoritative-open-item-snapshot-loader-spike.md), remaining tutarını explicit effective date ve recorded cutoff'a göre domain katmanında türetir. Late-recorded unallocation geçmiş kesime sızmaz; cross-company due-line görünmez.

Party report source contract kanıtı (27 Ağustos 2026): Reporting'in Party tablolarını doğrudan okumaması için bağımsız `Parties.Contracts` yüzeyi [contract spike planında](../project/plans/2026-08-27-party-report-source-contract-spike.md) tanımlandı. Contract explicit as-of/cutoff, opening exposure, balance side, control account, currency, watermark/checksum, open-item ve immutable impact fact'lerini taşır. Kaynakta dispute/block kanıtı yoksa `Unavailable` açık kalır ve `Clear` varsayılmaz.

Party report projection builder kanıtı (27 Ağustos 2026): Source contract başlangıç olayının source type, effective date ve recorded-at kanıtlarıyla genişletildi; Reporting application builder statement/aging modellerini kaynak tablolara erişmeden üretir. Exact remaining/closing cross-foot ve unavailable restriction fail-closed davranışı [builder planında](../project/plans/2026-08-27-party-report-projection-builder-spike.md) doğrulandı. PartyAccount balance side, opening, impact posting lifecycle ve dispute/block kanıtı artık authoritative adapter tarafından explicit taşınır.

Open-item concurrency kapasite kanıtı (26 Ağustos 2026): Due-line bazlı transaction lock ve DB net-capacity guard'ı [teknik spike](../project/plans/2026-08-26-open-item-capacity-concurrency-guard-spike.md) kapsamında eklendi. Paralel 40 + 30 GBP allocation, 60 GBP original due-line'ı aşamadı; owner-tamper 41 GBP write-off, 40 GBP due-line üzerinde reddedildi. Runtime due-line UPDATE yetkisi almadan append-only sınır korundu.

## 17. Ek rapor ve kabul

- Müşteri/tedarikçi statement’ı kaynak belge, vade, payment, allocation/unallocation ve kalan zincirini gösterir.
- AR/AP aging taksit bazlı ve belge/para/şirket/party toplamlarına cross-foot eder.
- Control-account raporu subledger ile GL’yi aynı as-of, currency ve dimension’da karşılaştırır.
- Bir ödeme üç faturaya dağıtılır, bir bağ kaldırılır ve yeniden tahsis edilirken ödeme/GL toplamının değişmediği test edilir.
- Credit note, avans, fazla ödeme, write-off, ihtilaflı kalem ve çok dövizli kısmi kapama golden data kapsamındadır.

## 18. Kagu Ltd. cari hesap ve vade politikası

- `PARTY-ACC-001`: Bir Party aynı anda müşteri ve tedarikçi olabilir; kimlik kopyalanmaz. Aynı Company ve currency içinde `Receivable` ve `Payable` için ayrı PartyAccount açılır ve her biri ayrı control-account snapshot'ı taşır.
- Kullanıcı görünür cari kodu chart mapping'den türetilen 120/320 ailesini gösterebilir; Party domain hesap numarasını hard-code etmez. Örnek ad `A Ticaret Ltd. (USD)` olabilir fakat ad benzersizlik veya authorization anahtarı değildir.
- Her PartyAccount tam bir TRY, USD, EUR veya GBP işlem para birimine bağlıdır. Aynı hesapta farklı işlem para birimi kullanılamaz; diğer para için ayrı hesap gerekir. Cross-account virman iki taraflı, kur snapshot'lı ayrı ekonomik olaydır.
- `PARTY-ACC-002`: Açılış bakiyesi cari kart alanı değildir. Yetkili kullanıcının oluşturduğu, effective date/recorded-at/source ve karşı GL posting'ini taşıyan ayrı append-only opening event'tir. Kritik opening import/posting hazırlayandan farklı yönetici onayı ister.
- Varsayılan payment term `cash/due-now`; cari açılırken peşin, 30, 60, 90 veya 120 takvim günü seçilebilir ve belge kendi payment-term snapshot'ını alır.
- Otomatik allocation önerisi vadesi en eski açık kalemden başlar; eşit vadede kaynak effective date ve immutable kimlikle deterministik sıralanır. Kullanıcı override permission ve gerekçeyle dağılımı değiştirebilir.
- Fazla ödeme payment'ı veya GL hareketini değiştirmez; `unapplied credit/advance` olarak kalır. Sonraki faturada en eski vade kuralıyla allocation önerilir, sessizce gelecekteki belgeye yazılmaz.
- Write-off ayrı event, reason, permission, hesap/rule snapshot ve hazırlayandan farklı tek yönetici onayı ister. Reversal ayrı counter-event'tir.
- Aging payment-term listesi değildir. Rapor bucket'ları `future`, `due-now`, `1–30`, `31–60`, `61–90`, `91–120`, `121+` takvim günüdür. Disputed/blocked kalemler toplam bakiyeye dahildir; ayrıca flag ve subtotal ile gösterilir.

Persistence güncellemesi (27 Ağustos 2026): `0030_party_account_balance_side_expand` migration'ı PartyAccount'a explicit `Receivable`/`Payable` sınıfı ekledi. Aynı Party + Company + currency için bir alacak ve bir borç hesabı birlikte açılabilir; aynı yönde ikinci hesap unique index ile reddedilir. Migration öncesi sınıflandırılmamış satırlar yanlış control-account tahminiyle backfill edilmez; `NULL` legacy satır olarak korunur ve yeni `NULL` insert'i `NOT VALID` check constraint sayesinde reddedilir. Due-schedule writer yeni hesapta balance side ister ve mevcut hesap kimliği/rolü/para/control-account eşleşmesini fail-closed doğrular. Boş DB, 29 migration + mevcut legacy satırlı DB, migration idempotency, aynı Party/GBP AR+AP, duplicate-role ve RLS kontrolleri gerçek PostgreSQL 18 üzerinde geçmiştir.

Açılış kaynağı persistence kanıtı (27 Ağustos 2026): `PARTY-ACC-002` için açılış tutarı PartyAccount üzerindeki değişebilir bir kolon yapılmadı. `party.account-opening` source type ve değişmez version `1` kullanan ayrı `party_account_opening_event`; debit/credit yönü, pozitif `numeric(20,4)` original amount, effective date, UTC recorded-at/actor ve PartyAccount'ın receivable/payable + currency + control-account snapshot'ını taşır. `party.opening-balance.create` permission'ı olmayan veya company scope dışındaki hazırlama fail-closed olur. Aynı event ID retry'ı yalnız bütün immutable içerik ve actor eşitse ilk sonucu verir; runtime rolü UPDATE/DELETE yapamaz. Composite FK, owner seviyesindeki doğrudan yazmada bile sahte rol/para/control-account snapshot'ını reddeder. Bu kayıt yalnız hazırlanan kaynak olayıdır: exact source/version için farklı yönetici approval'ı ve posted journal oluşmadan cari bakiyesi, ekstre açılışı veya aging toplamı üretmez.

Vade posting kimliği ve authoritative source adapter kanıtı (28 Ağustos 2026): `0032_party_due_source_posting_identity_expand`, due-schedule header'a kaynak `effective_date` ve canonical Accounting `posting_purpose` ekler. `NOT VALID` constraint 0032 öncesi satırları uydurma tarihle doldurmadan korur; yeni satırda iki alanı da zorunlu kılar. Loader eski sınıflandırılmamış satırı `DUE_SCHEDULE_POSTING_IDENTITY_UNAVAILABLE` ile durdurur. Parties adapter'ı repeatable-read/RLS kesitinde PartyAccount, opening ve due snapshot'larını yükler; Accounting tablosuna veya Infrastructure projesine referans vermeden exact posted-source evidence portuyla birleşir. Unposted veya aynı kesitte reversed kaynak etkisizdir; source/journal tarih-kimlik uyuşmazlığı ve aynı kaynağın birden fazla aktif sürümü fail-closed olur. Gerçek PostgreSQL zincirinde unposted kaynaklar dışlandı; posted `25 GBP` opening + `75 GBP` due doğru batch'e girdi; başka company görünmedi.

Open-item impact posting kimliği kanıtı (28 Ağustos 2026): `0033_open_item_impact_source_identity_expand`, allocation/unallocation/write-off impact'ine canonical source type, pozitif source version ve posting purpose ekler. Expand-compatible `NOT VALID` constraint pre-0033 satırlarını tahmini backfill yapmadan korur; yeni eksik kimlikli yazımı reddeder ve authoritative loader legacy satırı `OPEN_ITEM_IMPACT_POSTING_IDENTITY_UNAVAILABLE` ile durdurur. Accounting lifecycle okuması exact kaynağı `NotPosted`, `Active` veya original + reversal kanıtlı `Reversed` olarak aynı bitemporal kesitte açıklar. Original allocation/write-off ve counter unallocation/write-off reversal yalnız kendi exact aktif posting'leriyle remaining tutarına girer. Gerçek PostgreSQL rapor zincirinde posted `10 GBP` allocation, `75 GBP` açık kalemi `65 GBP` yaptı; unposted unallocation etkisiz kaldı ve post edilince kalan `75 GBP` olarak geri açıldı. Original journal ayrıca reversal ile pasifken counter aktif bırakılan çift-ters durum typed conflict ile reddedildi.

Open-item restriction kanıtı (28 Ağustos 2026): `0034_open_item_restriction_event`, ihtilaf ve tahsilat blokajını mutable flag yerine append-only `Applied`/`Released` olaylarıyla saklar; her olay zorunlu reason code, effective date, UTC recorded-at/actor ve exact due-line kapsamı taşır. Due-line kilidi aynı türden iki aktif restriction yarışını engeller; runtime role due-line UPDATE verilmeden güvenli owner-held trigger çalışması `0035` privilege migration'ıyla sağlanır. `party.open-item-restriction.manage` permission'ı uygulama komutunda zorunludur. As-of loader late-recorded release'i geçmiş kesime sızdırmaz; sıfır olay authoritative `Clear`, aktif durumlar `Disputed`, `Blocked` veya `DisputedAndBlocked` üretir. Gerçek PostgreSQL'de immutable replay, farklı payload conflict, duplicate-active reddi, release kesiti, append-only privilege ve cross-company negatifleri geçti. Bu kayıtlar bakiyeyi değiştirmez; aging toplamına dahil kalır ve ayrı flag/subtotal üretir.

Party→GL lineage ve kontrol hesabı kanıtı (29 Ağustos 2026): Authoritative source batch artık rapora giren her aktif Accounting sonucunun exact journal ID, source type/event/version/purpose ve effective/recorded/posted kesimini immutable `PostingLineage` olarak taşır; bu set source checksum V2 içine canonical sırayla dahildir. Reporting control-account portu yalnız report slice/control-account kimliği değil, bu exact Party source batch'ini de almak zorundadır. Gerçek PostgreSQL golden kesitinde posted `75 GBP` due source, statement ve aging'e `75 GBP`, fonksiyonel parası TRY olan journal'ın immutable transaction-currency snapshot'ından control-account raporuna yine `75 GBP` olarak yazıldı; generation ilk çalışmada oluştu, aynı command replay'inde yeni fact üretilmedi. Aynı fixture'ın `25 GBP` opening + `75 GBP` due + `10 GBP` allocation + `10 GBP` unallocation kesiti cari alt defter ve GL transaction-currency görünümünde `110 debit - 10 credit = 100 GBP` sıfır fark verdi. Yanlış company scope, yanlış işlem para birimi ve journal'da seçili control-account satırı bulunmaması fail-closed reddedildi.

Açık opening-aging kararı (29 Ağustos 2026): Mevcut opening event yalnız net statement opening exposure ve Accounting posting kimliği taşır; ayrı due date/payment-term/open-item kimliği taşımadığı için `25 GBP` opening'i aging bucket'ına veya allocation zincirine tahminle koymak mümkün değildir. Bu nedenle due-only source→statement/aging→GL publication kanıtı tamamlanmış, opening dahil control-account mutabakatı tamamlanmış olsa da opening dahil statement-aging golden kapısı açık kalır. Ürün politikası opening girişinin explicit due date/payment-term ile settle edilebilir open item üretip üretmeyeceğini belirlemeden `effective date = due date` varsayımı hard-code edilmez.

Opening-aging ürün kararı (31 Ağustos 2026): `DEC-MP01-021` ile yeni opening girişi bir veya birden fazla zorunlu vade/payment-term snapshot satırı taşır; satır toplamı opening source tutarına exact eşittir. Bu satırlar normal due open item olarak kısmi/tam allocation alır ve oldest-due sırasına katılır. UI effective date'i başlangıç vadesi olarak önerebilir fakat persisted vade açık kullanıcı girdisidir. Legacy opening olaylarına tarih uydurulmaz; yeni settleable opening PartyAccount doğal yönünde olmalıdır.
