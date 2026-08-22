# Muhasebe ve Büyük Defter Modülü

## 1. Amaç

Tüm alt defterlerden gelen mali olayları çift taraflı kayıt düzeninde, KKTC Tekdüzen Hesap Planı ile eşlenebilir, dönemsel ve denetlenebilir biçimde büyük deftere taşır. Muhasebe motoru hesap numaralarını iş koduna gömmez; sürümlü kayıt kuralları kullanır.

## 2. Ana model

- `chart_of_accounts`, `account`, `account_mapping`
- `fiscal_year`, `accounting_period`
- `journal`, `journal_line`
- `posting_rule`, `posting_rule_version`
- `accounting_event`, `posting_run`
- `dimension`, `dimension_value` (şube, masraf merkezi, proje vb.)
- `currency_rate`, `revaluation_run`
- `accrual_schedule`, `allocation_rule`
- `period_close`, `reopen_request`
- `subledger_reconciliation`

Hesap planı resmi kaynaktan veya onaylı şablondan içe aktarılır. Kaynak, sürüm, yürürlük tarihi ve kullanıcı değişiklikleri saklanır.

## 3. Muhasebeleştirme motoru

1. Kaynak modül `accounting_event` üretir.
2. Motor; şirket, olay türü, tarih, vergi sınıfı, mal/hizmet grubu, cari türü ve boyutlara göre yürürlükteki kayıt kuralını seçer.
3. Kural borç/alacak satırlarını, açıklamayı, boyut zorunluluğunu ve kur politikasını üretir.
4. Ön izleme doğrulanır; toplam borç = toplam alacak değilse kayıt reddedilir.
5. Başarılı fiş kaynak olay ile benzersiz bağ kurar.
6. Sonuç outbox olayı ve denetim kaydı üretir.

Kural değişikliği geçmiş fişleri değiştirmez. Her fiş kullandığı kural sürümünü ve vergi/kur anlık görüntüsünü taşır.

## 4. Değişmez kurallar

- `ACC-INV-001`: Her fişte şirket, para birimi ve yerel para bazında borç toplamı alacak toplamına eşittir.
- `ACC-INV-002`: Kesinleşmiş fiş satırı güncellenemez veya silinemez.
- `ACC-INV-003`: Düzeltme, kaynak fişe bağlanan ters kayıt ve gerekiyorsa yeni doğru kayıtla yapılır.
- `ACC-INV-004`: Kapalı döneme, açık istisna yetkisi ve kayıtlı yeniden açma kararı olmadan fiş yazılamaz.
- `ACC-INV-005`: Bir kaynak olay aynı şirket için en fazla bir etkin muhasebe sonucu üretir.
- `ACC-INV-006`: Her satır geçerli ve postalanabilir bir hesaba aittir; üst/toplam hesaba kayıt yapılamaz.
- `ACC-INV-007`: Gerekli boyutlar eksikse kayıt askıda kalır, varsayılan sessizce atanmaz.
- `ACC-INV-008`: Yerel para tutarı, işlem para birimi, kur ve yuvarlama farkı yeniden hesaplanabilir biçimde saklanır.

## 5. Manuel fişler

Manuel fiş taslak, onay ve postala aşamalarından geçer. Hazırlayan ile onaylayan, belirlenen tutarın üzerinde farklı kişilerdir. Dosya eki/gerekçe zorunluluğu fiş türüne göre ayarlanır. Toplu içe aktarma önce staging alanında doğrulanır; satır bazlı hata raporu olmadan kısmi postalanmaz.

## 6. Dönem işlemleri

- `ACC-PER-001`: Kapanış durumu `open → soft_close → review → hard_close` sırasını izler; geri geçiş normal state transition değildir ve onaylı reopen workflow snapshot'ı ister.
- `ACC-PER-002`: Operational, inventory valuation, GL, tax ve hard/legal kilitleri company + period + scope bazında ayrıdır; bir kapsamın açılması diğerini açmaz.
- `ACC-PER-003`: Standart posting, ilgili GL ve hard/legal kilit snapshot'ları açık değilse veya eksikse fail-closed reddedilir.

- açılış bakiyeleri ve önceki dönem devri,
- tahakkuk ve ters tahakkuk,
- masraf dağıtımı,
- yabancı para değerleme,
- amortisman sistem dışıysa kontrollü içe aktarma,
- alt defter–büyük defter mutabakatı,
- geçici kapanış, nihai kapanış ve yetkili yeniden açma,
- kapanış kontrol listesi ve elektronik imzalı/onaylı kanıt paketi.

Kapanış, açık istisnaları listeler; kritik mutabakatsızlık varken nihai kapanışı engeller.

## 7. Alt defter mutabakatları

En az:

- müşteri/tedarikçi cari bakiyesi ↔ ilgili büyük defter kontrol hesabı,
- stok değerlemesi ↔ stok hesapları,
- banka/kasa alt defteri ↔ banka/kasa hesapları,
- çek/senet portföyü ↔ çek/senet hesapları,
- KDV özeti ↔ vergi hesapları,
- açık kabul/fatura farkı ↔ geçici hesaplar.

Mutabakatlar `as_of` tarihi, kapsam, sorgu sürümü, fark ve çözüm durumuyla saklanır.

## 8. Mali raporlar

- mizan (dönem/aylık/kümülatif),
- yevmiye ve büyük defter,
- bilanço ve gelir tablosu,
- hesap hareket dökümü,
- nakit akış çalışma raporu,
- boyut bazlı kârlılık,
- dövizli bakiye ve değerleme,
- alt defter mutabakat raporları.

Her rapor şirket, mali dönem, para birimi, muhasebe kapsamı, üretim zamanı ve veri kesim zamanını gösterir. Rapor satırından kaynak fişe ve iş belgesine inilmelidir.

## 9. API ve yetkiler

- `POST /api/v1/accounting/events/{id}/preview`
- `POST /api/v1/accounting/events/{id}/post`
- `POST /api/v1/journals/{id}/reverse`
- `POST /api/v1/periods/{id}/close`
- `POST /api/v1/periods/{id}/reopen-requests`
- `GET /api/v1/trial-balance?period=...`

Roller: muhasebe kullanıcısı, kıdemli muhasebeci, finans yöneticisi, dönem yöneticisi, denetçi. Kayıt kuralı düzenleme, manuel fiş, postala ve dönem açma ayrı izinlerdir.

## 10. KKTC uyum kapısı

- Hesap planı sürümü resmi KKTC kaynağıyla ve yetkili mali müşavirle eşleştirilir.
- Mevzuat gerektiriyorsa yazılım/programcı onay veya kayıt süreçleri tamamlanmadan “mevzuata uyumlu” beyanı yapılmaz.
- Yasal defter biçimi, numaralama, saklama ve çıktı gereksinimleri canlı öncesi hukuki matriste kapatılır.
- Türkiye'ye ait Tekdüzen, e-Defter veya beyan varsayımları KKTC için otomatik kabul edilmez.

## 11. Kabul testleri

- Üretici/property testleriyle hiçbir fişin dengesiz oluşmaması.
- Aynı olayın paralel yeniden işlenmesinde tek fiş kalması.
- Kapalı dönem, yeniden açma ve ters kayıt senaryoları.
- Kural sürüm değişikliğinin eski fişleri değiştirmemesi.
- Çoklu para birimi, farklı kur ve yuvarlama sınır değerleri.
- Her alt defter için örnek veriyle sıfır fark mutabakatı.
- Şirket ve boyut yetkisinin satır/rapor seviyesinde korunması.
- Mizan–yevmiye–bilanço toplamlarının aynı veri kesiminde tutarlı olması.

## 12. Muhasebe çekirdeği katmanları

Kaynak iş modülü GL tablosuna yazmaz. PostingRequest şu bilgileri taşır:

- source economic event ve line kimliği;
- posting purpose ve effective/document date;
- company, functional/transaction currency ve rate snapshot;
- vergi/adjustment/party/item/warehouse bilgileri;
- accounting dimensions;
- seçilen posting rule version ve açıklama girdileri.

Posting engine, JournalEntry ve çok bacaklı JournalLine üretir. Bir transaction içinde fonksiyonel para borç=alacak; gerekiyorsa işlem para birimi/quantity dengesi ayrıca doğrulanır. Her line kaynak olay/line’a geri döner. Manual journal açık source class’tır; kaynak referansı, gerekçe, ek ve maker-checker olmadan post edilemez.

## 13. Özel günlükler, alt defter ve kontrol hesapları

Sales, purchase, cash receipts, cash disbursements ve general journal rapor sınıfları GL’nin ayrı gerçekleri değildir; kaynak olayları sınıflayan özel günlük görünümüdür. Cari, stok, banka ve çek alt defterlerinin her biri tanımlı GL control account’a mutabık olmalıdır.

Control-account reconciliation aynı company, currency, effective-date as-of ve dimensions üzerinde:

subledger opening + movements = subledger closing;
GL opening + debits − credits = GL closing;
subledger closing − GL closing = zero

sonucunu verir. Fark varsa source-less line, missing posting, duplicate generation, wrong dimension/rate veya timing farkı sınıflanır; suspense’a sessiz kapatma yapılmaz.

## 14. Tarih, kilit ve düzeltme

DocumentDate, EffectiveDate, RecordedAt ve PostedAt ayrıdır. Late-arriving document; GL/tax/inventory kilitlerini, cut-off ve açıklama politikasını kontrol eder. Hard-closed döneme geriye dönük yazmak yerine yetkili policy current correction period + original-period reference üretebilir; kural KKTC uzman onayına bağlıdır.

Kesin fiş reverse entry ile düzeltilir. Repost yalnız source olay doğru, türetilmiş journal/projection hatalı veya kural versiyonu için resmen yeniden üretim gerekliyse kullanılır. Dry-run, old/new line diff, rule version, closed period ve filed tax etkisi olmadan execute edilemez.

## 15. Kapanış ve zorunlu mali rapor seti

Kapanış sırası:

1. posting/integration exception ve numara boşlukları;
2. bank/cash, cheque, AR/AP ve inventory subledger mutabakatı;
3. GRNI, goods-in-transit, uninvoiced dispatch, accrual/prepayment;
4. kur değerleme, vergi çalışma dosyası ve manual journal review;
5. unadjusted trial balance → adjustment → adjusted trial balance;
6. P&L, balance sheet, cash flow mapping ve retained earnings/closing;
7. control evidence pack ve ayrı scope lock.

MVP raporları: chart of accounts, journal, GL detail, trial balance, balance sheet, P&L, cash-flow mapping/workpaper, account reconciliation, manual journal, source-to-GL audit trail ve period change report. Her biri comparative dönem, as-of/generation ve source drill-down taşır.
