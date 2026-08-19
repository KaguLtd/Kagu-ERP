# Veri Taşıma ve Veri Kalitesi Planı

## 1. Amaç

Mevcut Logo/Excel/başka ERP verisini yeni sisteme izlenebilir, mutabık ve geri dönülebilir biçimde taşımak. Taşıma yalnız ETL değildir; veri sahipliği, temizlik, mali açılış ve kanıt sürecidir.

## 2. Aşamalar

1. **Envanter:** kaynak sistem/sürüm, tablo/dosya, kayıt sayısı, tarih aralığı, sahip, hassasiyet.
2. **Profil:** boşluk, tekrar, biçim, kod seti, ilişkisel kırık, sıra dışı bakiye.
3. **Eşleme:** kaynak → hedef alan, dönüşüm, varsayılan, reddetme kuralı ve sahibi.
4. **Staging:** ham veri değişmeden, kaynak dosya hash'i ve satır kimliğiyle yüklenir.
5. **Temizleme:** iş sahibi kararları kural ve istisna listesi olarak uygulanır.
6. **Dönüşüm:** version-controlled migration pipeline hedef komut modelini üretir.
7. **Doğrulama:** teknik sayım + mali/operasyonel mutabakat.
8. **Deneme:** en az iki tam dress rehearsal ve süre ölçümü.
9. **Cutover:** yazma dondurma, son delta, onay ve yeni sistemi açma.
10. **Eski sistem:** süreli salt okunur erişim ve yasal arşiv; plansız silme yok.

## 3. Taşınacak veri sınıfları

- şirket/şube/depo ve kullanıcı eşlemeleri,
- Tekdüzen hesap planı ve boyutlar,
- cari hesaplar, adres/iletişim/vergi/banka bilgileri,
- stok kartı, birim, barkod, depo, lot/seri,
- açık satış/satın alma siparişleri,
- açık fatura, ödeme/tahsilat ve cari bakiye,
- banka/kasa bakiye ve mutabakat başlangıcı,
- çek/senet portföyü ve fiziki konum,
- stok açılış miktar/değer,
- muhasebe açılış mizan/fişleri,
- gerekliyse tarihsel belge ve ek arşivi.

Tüm tarihçeyi transactional modele taşımak varsayılan değildir. Yasal/iş ihtiyacına göre “özet açılış + salt okunur tarihsel arşiv” seçeneği değerlendirilir.

## 4. Eşleme sözleşmesi

Her alan için:

- kaynak sistem/tablo/kolon,
- hedef varlık/alan,
- veri tipi/format/encoding,
- dönüşüm ve lookup,
- boş/geçersiz davranışı,
- tekillik/dedup anahtarı,
- hukuki saklama sınıfı,
- örnek giriş/beklenen çıktı,
- iş sahibi/onaylayan

belgelenir. Kaynak kimliği `legacy_reference` olarak saklanır; her hedef kayıt staging satırına izlenebilir.

## 5. Veri kalite kuralları

- Cari: yinelenen VKN/ad/unvan şüphesi, eksik ülke/adres, geçersiz banka biçimi.
- Stok: birim dönüşüm çelişkisi, yinelenen barkod, negatif/karşılıksız lot.
- Muhasebe: borç=alacak, hesap var/aktif, dönem/tarih, boyut ve mizan toplamı.
- Cari: açık belge toplamı = açılış cari kontrol hesabı.
- Stok: miktar/değer = stok kontrol hesapları; yöntem farkı açıklanır.
- Çek: seri tekilliği, durum-vade-fiziki konum.
- Belge: tarih/numara/para/vergi toplamı ve tekrar.

Hata sınıfları: `blocker`, `requires_business_decision`, `auto_fix_with_evidence`, `warning`, `accepted_exception`.

## 6. Mali mutabakat paketi

Cutover tarihinde kaynak ve hedef için:

- mizan hesap bazında,
- müşteri/tedarikçi yaşlandırma ve toplam,
- stok depo/mal bazında miktar ve değer,
- banka/kasa hesap bakiyesi,
- çek/senet durum/tutar,
- açık sipariş/taahhüt,
- KDV/vergi hesapları,
- belge sayısı ve toplamları

karşılaştırılır. Fark, tolerans, açıklama, sahibi ve onayı olmayan geçiş yapılamaz. “Toplam doğru” yeterli değildir; örnek ve riskli satır drill-down yapılır.

## 7. Açılış stratejisi

- Ana veriler cutover öncesi birden çok kez yüklenebilir; son delta uygulanır.
- Finansal açılış, onaylı tarih ve kapalı kaynak döneminden alınır.
- Açık belgeler mümkünse tek tek; yalnız bakiye taşınacaksa tahsis/yaşlandırma etkisi belgelenir.
- Stok miktar ve maliyet yöntemi birlikte taşınır; yalnız miktar yeterli değildir.
- Eski resmi e-fatura numarası yeni sistemce yeniden üretilmez; arşiv/referans olarak taşınır.
- Yeni sistem iş numarası ile legacy belge numarası ayrı alanlardır.

## 8. Cutover runbook'u

- go/no-go ve sorumlu listesi,
- kaynak yazma dondurma zamanı,
- son yedek/hash ve delta çıkarımı,
- staging/yükleme komut ve süreleri,
- otomatik doğrulama raporu,
- mali/operasyonel imza,
- kullanıcı/entegrasyon açılışı,
- ilk gün smoke ve yoğun destek,
- geri dönüş karar saati/ölçütleri.

Geri dönüş, yeni sistemde yaratılan işlemlerin kaybolmadan nasıl korunacağını belirtir. Cutover sonrası iki sistemde paralel yazma varsayılan olarak yasaktır.

## 9. Güvenlik

- Kaynak export şifreli, sınırlı erişimli ve hash'li.
- Prod veri geliştirici cihazına indirilmez.
- Dönüşüm logu PII'yi maskeleyerek satır kimliği kullanır.
- Geçici staging/export saklama süresi ve güvenli imhası vardır.
- Taşıma service account'u süreli ve en az yetkilidir.
- Tüm manuel düzeltmeler kim/ne/neden ve önce/sonra ile kayıtlıdır.

## 10. Kabul

- İki başarılı tam prova ve tahmin edilen pencereye sığma.
- Blocker veri kalite hatası sıfır.
- Mali mutabakat farkları sıfır veya imzalı, açıklanmış tolerans içinde.
- Satır düzeyi kaynak-hedef izlenebilirliği örneklenmiş.
- Kullanıcı kabul örneklemi tamamlanmış.
- Cutover/rollback ve legacy erişim planı prova edilmiş.
- Taşıma kodu, mapping ve raporlar release artefaktına bağlanmış.

## 11. Kaynak olay ve açık kalem taşıma ayrıntısı

Migration, yalnız cari/hesap “bakiye” kolonlarını taşımaz. Seçilen stratejiye göre:

- party canonical identity ve customer/supplier roles;
- document header/line ve order–dispatch–invoice links;
- due schedule/open items;
- payment/credit ve allocation/unallocation;
- bank statement/reconciliation;
- stock movement, lot/serial ve cost layers;
- cheque/senet event/custody/allocation;
- journal source references, dimensions ve reversal

ilişkilerini korur.

Detay geçmiş alınamıyorsa açılış yaklaşımı açıkça belgelenir: cutover date itibarıyla onaylı open-item, stock quantity/value, bank/cash, instrument portfolio ve GL opening entries. “Bakiye geldi” tam tarihçe gibi gösterilmez; source system/closing report/hash referansı taşır.

## 12. Mapping ve staging kanıtı

Her source record staging’de source system, company/period, source key/version, raw hash, extraction timestamp ve mapping version taşır. Invalid/duplicate/unmapped satırlar sessiz düşmez. Party/item/account crosswalk many-to-one merge ve one-to-many split kararlarını owner/reason ile kaydeder.

Logo veya başka ERP tablo adı önceden varsayılmaz; izinli export/API/schema discovery ile profil çıkarılır. Telif/lisans ve kişisel veri sınırı gözetilir; production bağlantısı kalıcı entegrasyona dönüşmez.

## 13. Mutabakat merdiveni

1. Dosya/table satır sayısı ve control totals.
2. Master-data accepted/rejected/merged sayıları.
3. Belge header/line count ve net/tax/gross.
4. Open items = original − allocations; aging bucket.
5. Stock quantity + valuation by item/warehouse/lot.
6. Bank/cash/cheque portfolio.
7. Journal debit=credit ve account/dimension trial balance.
8. Subledger control accounts = GL.
9. Balance sheet/P&L/cash-flow workpaper ve retained earnings.

Her fark amount, currency, source/target IDs, reason class, materiality, owner ve disposition taşır. “Round-off” sınıfı threshold/rule olmadan kullanılmaz.

## 14. Cutover ve paralel kapanış

Extraction freeze, delta capture, final posting cutoff ve number sequence handoff kesin zaman çizelgesidir. Geç gelen belge hangi sisteme ve hangi effective/correction period’a gireceğiyle planlanır.

En az iki paralel kapanış; kaynak ve hedefte aynı cut-off, kur, rule ve kapsamla karşılaştırılır. Legacy read-only erişim/saklama, audit export ve restore sorumlusu tanımlanmadan eski sistem kapatılmaz.
