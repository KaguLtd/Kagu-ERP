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

`ACC-INV-005` için canonical source identity önce append-only bir PostgreSQL rezervasyonuyla yarışa karşı korunabilir. Rezervasyonun unique anahtarı tenant, company, source type, source event ve posting purpose alanlarıdır; içerik hash'i aynı anahtarın farklı taslakla yeniden kullanılmasını ayırt eder. Bu kayıt journal değildir ve posting yetkisi veya mali bakiye üretmez. Gerçek posting uygulandığında rezervasyon, journal ve outbox aynı transaction'da tamamlanmalı; rollback halinde yarım rezervasyon kalmamalıdır.

Rezervasyon adaptörü yalnız doğrulanmış journal draft kabul eder ve transaction sahipliğini caller'da bırakır. Aynı V1 fingerprint ile retry ilk reservation kimliğini döndürür; aynı canonical source identity farklı fingerprint ile gelirse fail-closed conflict üretir. Fingerprint sürümü sessizce değiştirilemez; algoritma değişimi mevcut rezervasyonlarla uyumluluk ve migration kararı ister.

Doğrulanmış journal taslağı, rezervasyona bağlı ayrı header ve line tablolarında append-only bir teknik snapshot olarak saklanabilir. Bu snapshot posted journal değildir ve mali bakiye üretmez. Runtime rolü yalnız `SELECT`/`INSERT` yetkisine sahiptir; tutarlar SQL öncesinde `numeric(20,4)` ölçek ve aralığına kayıpsız uyum için kontrol edilir. Rezervasyon, taslak header/line, audit ve outbox caller-owned tek transaction içinde birlikte commit veya rollback olur.

Application posting-candidate kapısı, `accounting.journal.post` iznini company scope içinde denetler ve hesap, boyut, kur ile dönem doğrulamalarının aynı draft'a ait olmasını zorunlu kılar. GL ve hard/legal lock snapshot'ları açık değilse fail-closed davranır. Bu aday posted journal değildir; authoritative period/date lookup, approval ve transaction-bound posted persistence tamamlanmadan mali sonuç üretemez.

Journal preparation orchestrator, authoritative effective-date dönemini ve lock state'lerini aynı caller-owned PostgreSQL transaction'ında yükler; ardından permission/account/dimension/currency candidate, kaynak rezervasyonu, immutable validated draft, authorization audit ve `accounting.journal-draft-prepared.v1` outbox olayını sıralı yürütür. Audit context trusted execution scope ile birebir eşleşmelidir. Orchestrator transaction açmaz veya commit etmez; hata/rollback hiçbir kısmi fact bırakmaz. Bu akış yalnız non-posted preparation'dır ve posted journal ya da GL satırı üretmez.

Preparation request caller tarafından oluşturulmuş account, dimension veya currency validation sonucu kabul etmez. Permission kanıt tablolarına erişmeden önce denetlenir; dönem, hesap, boyut ve kur kanıtlarının tamamı aynı transaction içinde authoritative PostgreSQL kaynaklarından yüklenir. Public API ancak kaynak belge application contract'ı server tarafında canonical draft üretebildiğinde bu orchestrator'a bağlanabilir; istemcinin iç journal snapshot göndermesine izin verilmez.

Canonical source portuna giren preparation command journal draft veya mali snapshot taşımaz; yalnız trusted scope/audit, source identity ve server-generated işlem kimliklerini taşır. Source adapter aynı transaction içinde canonical draft ile chart version döndürür ve sonuç komuttaki tenant/company/source type/source event/posting purpose kimliğiyle birebir eşleşmelidir. Permission source adapter çağrısından önce denetlenir. Gerçek belge adapter'ı ve posting rule seçimi ilgili modül ile onaylı mali politika tarafından sağlanmadan public posting/preparation endpoint'i açılmaz.

Command ayrıca pozitif expected source version taşır. Source adapter authoritative belge sürümünü sonucu içinde döndürür; sürüm birebir eşleşmezse preparation hiçbir mali veya teknik fact yazmadan `JOURNAL_SOURCE_VERSION_MISMATCH` ile durur. Böylece retry veya approval aralığında değişmiş belge sessizce farklı journal draft üretemez.

Idempotent preparation composition request hash'ini tenant/company/source identity/posting purpose/expected source version alanlarından üretir. İlk çağrıda idempotency acquire, canonical preparation ve completed response snapshot aynı transaction'dadır. Aynı key/payload replay'i source adapter'ı yeniden çağırmadan ilk response'u döndürür; farklı source version aynı key altında çatışır. Caller rollback idempotency kaydı, reservation, draft, audit ve outbox'ın tamamını birlikte geri alır.

Authoritative standard-posting dönem kapısı effective date'i PostgreSQL dönem aralığında çözer; sıfır veya birden çok eşleşmeyi reddeder. Bulunan dönem için canonical transaction advisory lock alınır, dönem eşleşmesi yeniden okunur ve GL ile hard/legal current lock state'leri doğrulanır. Dönem close/reopen yazma akışları aynı advisory lock protokolünü kullanmadan state değiştiremez. Runtime rolü dönem tablolarında yalnız `SELECT` yetkilidir; authoring ve transition workflow'u ayrı permission, approval, audit ve optimistic version kontrolü tamamlanana kadar açılmaz.

Authoritative account evidence loader, journal satırlarındaki tüm distinct hesapları seçilen immutable chart-of-accounts version içinden tenant/company scope ile yükler. Eksik hesap kanıtı, pasif hesap ve summary/non-posting hesap fail-closed reddedilir. Runtime rolü chart version ve account posting snapshot tablolarında yalnız `SELECT` yetkilidir. Bu teknik model hesap kodu, resmi KKTC chart içeriği veya authoring/activation yetkisi tanımlamaz.

Authoritative dimension evidence loader, journal'ın posting-rule version kimliğine ait immutable requirement setini ve required dimension kimliklerini tenant/company scope içinde yükler. Set yokluğu veya herhangi bir satırdaki eksik required dimension fail-closed reddedilir; sessiz varsayılan atanmaz. Runtime requirement tablolarında yalnız `SELECT` yetkilidir; dimension authoring ve default politikası bu kanıt modelinin dışındadır.

Authoritative currency evidence loader, journal draft'ındaki exchange-rate ve rounding-policy snapshot'larını tenant/company scope içinde immutable PostgreSQL kanıtıyla birebir eşleştirir. Eksik, değiştirilmiş veya başka şirkete ait kanıt fail-closed reddedilir; parasal oranlar `numeric(28,12)` ile saklanır ve binary floating point kullanılmaz. Runtime evidence tablolarında yalnız `SELECT` yetkilidir. Kur sağlayıcısı seçimi, oran import/yayımlama workflow'u ve mali politika onayı bu teknik kanıtın dışındadır.

Canonical journal preparation approval subject'ini source type, source event id ve expected source version'dan server-side türetir. Exact-version authoritative completed approval aynı PostgreSQL transaction'ında ve reservation/draft/audit/outbox'tan önce yüklenir; eksik veya eski sürümlü approval hiçbir journal fact üretmeden fail-closed davranır. Caller approval snapshot veya farklı subject kimliği sağlayamaz. Bu preparation hâlâ posted GL sonucu değildir.

Journal posting composition, approval-gated canonical preparation sonucunu immutable posted journal'a aynı caller-owned PostgreSQL transaction'ında taşır ve preparation audit/outbox'ından ayrı posted audit/outbox fact'leri üretir. API idempotency kaydı posting'den önce acquire edilir ve yalnız bütün zincir başarılı olduğunda final posted response snapshot'ıyla aynı transaction içinde tamamlanır. Completed replay source/approval/posting zincirini yeniden çalıştırmadan ilk fiş sonucunu döndürür; changed payload conflict olur. Posted outbox yazımı dahil herhangi bir adım başarısızsa caller commit edemez ve idempotency dahil bütün fact'ler rollback edilir. Bu teknik akış public endpoint, yasal yevmiye numarası veya reversal politikası tanımlamaz.

Posted journal persistence, validated draft'ı immutable header ve GL line snapshot'ına kopyalar; header aynı tenant/company kapsamındaki draft, period ve exact source-version approval'a FK ile bağlıdır. Runtime yalnız `SELECT/INSERT` yetkilidir. Deferred DB constraint trigger'ları commit anında line count ile debit/credit toplamlarını header'a cross-foot eder ve dengesiz sonucu reddeder. Internal `journal_id` yasal yevmiye numarası değildir; resmi numaralama ayrı onaylı policy ister.

Transaction-bound audit ve outbox adaptörleri aynı connection/transaction içinde çağrıldığında rezervasyon, audit kanıtı ve dış olay niyeti atomik kalır. Bu üç kaydın birlikte bulunması yine posted journal anlamına gelmez; journal header/line persistence ve tüm posting kapıları ayrıca tamamlanmalıdır.

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

Posted reversal bağı original ve counter journal'ı tenant/company scope içinde immutable olarak ilişkilendirir. Bir original yalnız bir reversal alabilir; iki bağlantılı yarışta PostgreSQL unique lock tek kazanan üretir. DB guard line number, account, source line, dimensions, functional currency, debit/credit ve currency calculation snapshot'ının exact inverse olduğunu doğrular; reversal chain, update ve delete fail-closed'dur. Bu persistence kanıtı reversal tarihini, correction period'ı, permission/approval veya public command'ı seçmez.

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
