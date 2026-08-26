# Banka ve Kasa Modülü

## 1. Amaç

Banka hesapları, kasalar, tahsilat/ödeme, hesaplar arası transfer, ekstre içe aktarma, işlem eşleştirme ve mutabakatı tek ve denetlenebilir bir alt defterde yönetir. İlk sürümün güvenilir tabanı dosya/API adaptörüdür; belirli bir bankanın çevrim içi bankacılık yeteneği varsayılmaz.

## 2. Ana varlıklar

- `bank`, `bank_branch`, `bank_account`
- `cash_account`, `cash_session`, `cash_count`
- `payment_instruction`, `payment_batch`
- `receipt`, `payment`, `bank_transfer`
- `statement_import`, `statement_line`
- `reconciliation_session`, `reconciliation_match`
- `exchange_rate_snapshot`, `bank_fee`

Banka hesabı bilgileri maskeleme sınıfına tabidir. IBAN/hesap numarası değişikliği, onaylı tedarikçi veya şirket ana verisi üzerinden yapılır ve güvenlik olayı olarak günlüğe alınır.

## 3. İş akışları

### 3.1 Tahsilat ve ödeme

`draft → submitted → approved → sent/realized → reconciled`

Başarısız veya reddedilen banka işlemi, aynı kayıt üzerinde gerçekleşmiş gibi işaretlenmez; ayrı sonuç olayı eklenir. Ödeme talebini hazırlayan ile nihai onaylayan, politika limitinin üzerinde aynı kişi olamaz.

### 3.2 Ekstre içe aktarma

1. Dosyanın hash'i, banka hesabı, dönem ve kaynak tipi kaydedilir.
2. Zararlı dosya ve biçim doğrulaması yapılır.
3. Satırlar bankaya özel adaptörden ortak modele çevrilir.
4. Benzersiz banka referansı varsa ana tekilleştirme anahtarıdır; yoksa tarih/tutar/açıklama/bakiye tabanlı güvenli parmak izi kullanılır.
5. Yinelenen dosya/satır yeniden finansal hareket üretmez.
6. Eşleştirme önerileri skoruyla sunulur; belirsiz sonuç kullanıcı onayı ister.

### 3.3 Mutabakat

Bir satır birden fazla ERP hareketine veya bir ERP hareketi birden fazla banka satırına, kontrollü çoklu eşleştirmeyle bağlanabilir. Toplam fark toleransı sıfır veya politika ile tanımlıdır. Dönem kapatıldığında eşleştirme değişikliği için yeniden açma yetkisi gerekir.

## 4. Değişmez kurallar

- `BNK-INV-001`: Kesinleşmiş nakit/banka hareketi silinemez; ters kayıtla düzeltilir.
- `BNK-INV-002`: Her ödeme/tahsilat para birimi, işlem kuru, yerel para karşılığı ve kur kaynağını taşır.
- `BNK-INV-003`: İçe aktarılan aynı banka satırı ikinci kez muhasebeleştirilemez.
- `BNK-INV-004`: Transferin kaynak ve hedef bacakları aynı korelasyon kimliğine sahiptir ve dengelidir.
- `BNK-INV-005`: Mutabık hareketin değişmesi yeniden mutabakat gerektirir.
- `BNK-INV-006`: Kasa bakiyesi, kesinleşmiş hareketlerin toplamıdır; elle bakiye yazılamaz.
- `BNK-INV-007`: Ödeme dosyası üretme ve bankaya gönderme yetkileri ayrı atanabilir.
- `BNK-STMT-001`: Normalize edilmiş ekstre satırı tenant, company, treasury account ve kanonik dış işlem kimliği kapsamında tekildir; yeniden import ikinci mali satır üretmez.
- `BNK-REC-001`: Reconciliation önerisi, ekstre satırı ve iç hareketi değiştirmeyen ayrı bir eşleştirme gerçeğidir.
- `BNK-REC-002`: Bir reconciliation önerisindeki eşleştirmeler aynı tenant/company/treasury account/para kapsamında kalır; toplam eşleşen tutar ekstre satırı veya iç hareket kapasitesini aşamaz.

## 5. Eşleştirme motoru

Kurallar; tutar, para birimi, valör tarihi aralığı, banka referansı, cari hesap, belge numarası ve açıklama belirteçlerini kullanır. Her öneri:

- eşleşen adayları,
- kural sürümünü,
- skoru ve skorun nedenlerini,
- otomatik/manuel kararını,
- karar veren kullanıcıyı

saklar. Sadece yüksek güven ve benzersiz aday bulunan, finans ekibinin etkinleştirdiği kurallar otomatik sonuçlandırabilir.

## 6. Kasa yönetimi

- Her kasa, para birimi ve sorumlu kullanıcıyla tanımlanır.
- Vardiya/oturum açılış ve kapanış sayımı desteklenir.
- Sayım farkı onay gerektirir ve muhasebe politikasıyla kaydedilir.
- Negatif kasa, şirket politikası izin vermiyorsa işlem anında engellenir.
- Nakit limitleri ve beklenmeyen yüksek hareketler uyarı üretir.

## 7. API ve ekranlar

- `POST /api/v1/payments/{id}/submit`
- `POST /api/v1/payment-batches/{id}/approve`
- `POST /api/v1/bank-statements/imports`
- `POST /api/v1/reconciliations/{id}/matches`
- `POST /api/v1/cash-sessions/{id}/close`
- `GET /api/v1/treasury/position?asOf=...`

Ekranlar: günlük nakit pozisyonu, ödeme çalışma masası, ekstre içe aktarma sihirbazı, mutabakat ekranı, kasa sayımı ve banka/kasa hareket detayı.

## 8. Muhasebe bağlantıları

- Tahsilat: banka/kasa borç; cari alacak kapama veya avans alacak.
- Ödeme: cari borç kapama/gider; banka/kasa alacak.
- Masraf/komisyon: banka masrafı ve varsa vergi şablonu.
- Kur farkı: gerçekleşmiş kur farkı şablonu.
- Transfer: transit hesap kullanımı politika ile seçilir.

Muhasebeleştirme idempotenttir ve kaynak işlem kimliğine göre tek kayıt üretir.

## 9. Güvenlik

- Tedarikçi banka hesabı değişikliği ödeme akışını geçici olarak bloke edebilir.
- Yeni alıcıya veya limit üstü ödemeye yeniden kimlik doğrulama/MFA uygulanabilir.
- Hesap numarası listelerde maskelenir; tam görüntüleme ayrı izne bağlıdır.
- Banka dosyaları karantina alanından işlenir.
- Banka API anahtarları ve imza sertifikaları secret store'da tutulur; loglanmaz.

## 10. Raporlar ve testler

Raporlar: günlük likidite, vade bazlı nakit tahmini, banka/kasa defteri, mutabakat farkı, bekleyen ödeme, banka masrafı, para birimi pozisyonu.

Zorunlu testler:

- aynı ekstreyi ve satırı yeniden yükleme,
- çoktan-bire ve birden-çoğa mutabakat,
- eşzamanlı ödeme onayında çift harcama koruması,
- kur/valör/komisyon muhasebesi,
- görevler ayrılığı ve hesap maskeleme,
- kasa açılış/kapanış farkları,
- başarısız bankaya gönderim sonrası güvenli yeniden deneme.

## 11. Ödeme, transfer ve banka kesinleşme durumları

Payment yaşam döngüsü ile banka durumu ayrıdır:

- `BNK-PAY-001`: Payment kendi tenant/company/source kimliğiyle ayrı ekonomik olaydır; allocation, banka kesinleşmesi ve reconciliation payment kaydının alanları veya durum eş anlamları değildir.
- `BNK-PAY-002`: Aynı company ve canonical source identity aynı posting purpose için en fazla bir payment ekonomik olay niyeti üretir.

- draft → approved → posted: iç nakit/banka hareketi kaydedildi;
- submitted/in_transit: bankaya iletildi veya bankadan kesinleşme bekliyor;
- reconciled: statement line ile onaylı eşleşti;
- returned/rejected: banka karşı olay/masrafıyla geri döndü.

Fatura allocation durumu bu zincirden ayrıdır. Ödeme posted olabilir fakat henüz faturaya tahsis edilmemiş veya bankada kesinleşmemiş olabilir. “Paid” etiketi yalnız tanımlı rapor/UI policy’sinden üretilir.

Transit/outstanding receipts ve outstanding payments GL hesapları company/bank/journal policy’sine bağlıdır. Reconciliation gerçekleşince transit hesap banka ana hesabına kapanır; tek payment iki kez bankaya yazılmaz.

Payment economic-event persistence teknik kanıtı (26 Ağustos 2026): Same-currency domain-validated payment draft'ı canonical source/purpose uniqueness ve bütün identity-rate snapshot alanlarıyla [append-only PostgreSQL persistence spike'ında](../project/plans/2026-08-26-payment-economic-event-persistence-spike.md) kanıtlandı. İki connection yarışında tek event oluştu; changed identity conflict, cross-company RLS ve runtime UPDATE/DELETE reddi geçti. Bu teknik snapshot approval, posted banka/kasa hareketi, settlement, reconciliation veya allocation kullanılabilirliği değildir.

Authoritative payment loader kanıtı (26 Ağustos 2026): Payment, canonical source ve identity-rate snapshot alanlarını aynı transaction/company scope içinde okuyup Treasury domain invariantlarından yeniden geçiren [salt-okunur loader](../project/plans/2026-08-26-authoritative-payment-economic-event-loader-spike.md) gerçek PostgreSQL'de doğrulandı. Cross-company payment ID fail-closed `null` döner; loader lifecycle state veya allocation kullanılabilirliği üretmez.

## 12. Ekstre bütünlüğü ve ISO 20022 hazırlığı

Statement/StatementLine modeli CSV, MT940, OFX veya ISO 20022 camt.053 adaptörlerinden bağımsız kanoniktir. MVP’de format desteği banka örnek dosyasıyla onaylanır; ISO 20022 ismi kullanmak tüm bankaların aynı profil olduğu anlamına gelmez.

Her import şunları saklar:

- original encrypted object, SHA-256, provider/bank/profile version;
- account identity, statement ID/sequence, period;
- opening/closing ve available balances;
- line booking date, value date, amount/currency, bank reference, counterparty ve remittance;
- parser version, control counts/totals ve duplicate decision.

Normalized statement-line persistence kanıtı (26 Ağustos 2026): Canonical external identity, signed amount/currency, booking/value date, raw SHA-256 ve parser version snapshot'ı [append-only PostgreSQL spike'ında](../project/plans/2026-08-26-statement-line-persistence-spike.md) kalıcılaştırıldı. Aynı external identity retry ve iki-connection yarışında tek satır üretir; changed identity payload, cross-company okuma ve runtime UPDATE/DELETE reddedilir. External-key türetimi ve dosya/parser güvenliği bu teknik tablonun değil, sürümlü adapter/import pipeline'ın sorumluluğudur.

Aynı dosya veya bank transaction tekrarında yeni hareket yaratılmaz. Raw payload mali domain tablosuna doğrudan yazılmaz; normalize mapping lineage taşır.

## 13. Mutabakat seti ve kontroller

ReconciliationSet durumları draft → submitted → approved veya rejected’tır. Öneri skoru yalnız yardımcıdır; yüksek riskli/çoklu eşleşme insan onayı ister. Approver, match önerisini yaratan veya kaynak payment’ı değiştiren kişi olamaz.

Match bir-to-one, one-to-many ve many-to-one olabilir; toplam/tolerans, currency, tarih ve reference açıklanır. Fark; banka masrafı, faiz, kur farkı, chargeback veya suspense event’i olarak ayrı onaylı kaynak olay üretir.

Kapanış kontrolü:

opening balance + signed statement lines = closing balance;
approved statement total = reconciled internal movements + açık farklar;
bank GL control account = reconciled/as-of bank subledger.

Raporlar unreconciled age, duplicate/import error, transit payment, stale proposal, suspense ve maker-checker exception içerir.
