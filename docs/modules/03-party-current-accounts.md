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
- `PartyAccount`: Company + party + currency/settlement policy.
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

## 16. Payment allocation defteri

Payment ekonomik nakit olayıdır; PaymentAllocation ödeme/kredi/mahsup ile DueScheduleLine arasındaki bağdır. Allocation:

- ödeme para birimi, açık kalem para birimi, fonksiyonel tutar ve kullanılan kuru snapshot eder;
- payment usable amount ve open item remaining amount sınırını aşamaz;
- bir ödeme çok açık kaleme, bir açık kalem çok ödemeye bağlanabilir;
- unallocation ile karşılanır; kaynak payment veya GL satırı silinmez;
- avans/fazla ödeme için unapplied credit olarak açık kalabilir;
- write-off, iskonto ve kur farkını ayrı reason/account/rule ile üretir.

Settlement terimi kullanıcı yüzünde üst kavram olabilir; veri modelinde allocation, netting, write-off ve bank reconciliation ayrı event type’tır.

## 17. Ek rapor ve kabul

- Müşteri/tedarikçi statement’ı kaynak belge, vade, payment, allocation/unallocation ve kalan zincirini gösterir.
- AR/AP aging taksit bazlı ve belge/para/şirket/party toplamlarına cross-foot eder.
- Control-account raporu subledger ile GL’yi aynı as-of, currency ve dimension’da karşılaştırır.
- Bir ödeme üç faturaya dağıtılır, bir bağ kaldırılır ve yeniden tahsis edilirken ödeme/GL toplamının değişmediği test edilir.
- Credit note, avans, fazla ödeme, write-off, ihtilaflı kalem ve çok dövizli kısmi kapama golden data kapsamındadır.
