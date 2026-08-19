# KKTC Vergi ve Uyum Modülü

## 1. Amaç ve yaklaşım

KKTC vergi kurallarını işlem kodundan ayıran, yürürlük tarihli ve kanıtlanabilir bir kural katmanı sağlar. Yazılım vergi danışmanının yerini almaz; resmi kaynak ve yetkili mali müşavir tarafından onaylanan yapılandırmayı uygular.

Temel ilke: KDV oranı, istisna, tevkifat/özel matrah, belge zorunluluğu veya beyan alanı kaynak koda sabitlenmez. Her kuralın yasal dayanağı, yayımlanma/yürürlük tarihi, onaylayan kişi ve test senaryosu bulunur.

## 2. Ana varlıklar

- `tax_type`, `tax_code`, `tax_rate_version`
- `tax_category`, `exemption_reason`
- `tax_rule`, `tax_rule_version`
- `tax_determination`, `tax_snapshot`
- `tax_period`, `tax_workbook`
- `tax_reconciliation`, `tax_adjustment`
- `legal_reference`, `compliance_evidence`
- `declaration_export`, `submission_record`

## 3. Vergi belirleme motoru

Girdiler:

- işlem ve belge türü,
- mal/hizmet vergi kategorisi,
- alıcı/satıcı statüsü ve ülke/bölge,
- teslim/hizmet yeri,
- işlem, belge ve vergiyi doğuran olay tarihi,
- para birimi/matrah,
- istisna belgesi veya ruhsat,
- şirket/şube ve faaliyet türü.

Çıktılar:

- uygulanacak vergi kodu/oran sürümü,
- matrah ve vergi hesaplama yöntemi,
- istisna/özel durum kodu,
- yuvarlama yöntemi,
- muhasebe hesap sınıfı,
- e-fatura alanları,
- karar açıklaması ve yasal referans.

Belge sonuçlandırılırken tam `tax_snapshot` alınır; sonraki kural değişimi eski belgeyi yeniden hesaplamaz.

## 4. Kural yaşam döngüsü

`draft → reviewed → approved → scheduled → active → retired`

- Etkin dönemler çakışamaz.
- Geçmişe etkili kural, etkilenen işlem analizi ve yetkili onayı olmadan etkinleşemez.
- İki kişi kural hazırlama/onay görevini paylaşır.
- Her sürüm, örnek işlemlerden oluşan regresyon paketi geçmeden yayınlanamaz.
- Resmi duyuru PDF/URL'si ve içerik hash'i kanıt olarak eklenir.

## 5. KDV çalışma alanı

Vergi dönemi çalışma alanı en az şunları verir:

- hesaplanan ve indirilecek KDV dökümleri,
- vergi kodu/oranı/istisna bazlı toplam,
- satış, satın alma, iade ve düzeltme ayrımı,
- muhasebe vergi hesaplarıyla mutabakat,
- eksik VKN, geçersiz tarih, negatif/aykırı matrah kontrolleri,
- e-fatura kayıtlarıyla çapraz kontrol,
- önceki dönem devri ve yetkili düzeltme,
- çıktı/beyan dosyası ile kaynak satır arasındaki iz.

Beyan portalına otomatik gönderim ancak resmi ve onaylı entegrasyon sözleşmesi varsa yapılır; aksi halde doğrulanmış çıktı ve kullanıcı tarafından kaydedilen gönderim makbuzu kullanılır.

## 6. Değişmez kurallar

- `TAX-INV-001`: Her sonuçlandırılmış belge bir vergi kuralı sürümüne ve anlık görüntüye bağlıdır.
- `TAX-INV-002`: Aynı matrah için aynı vergi kalemi iki kez hesaplanamaz.
- `TAX-INV-003`: Vergi toplamı satır/başlık yuvarlama politikasına göre yeniden üretilebilir olmalıdır.
- `TAX-INV-004`: İstisna kodu gerektiren işlemde dayanak/kanıt olmadan belge sonuçlandırılamaz.
- `TAX-INV-005`: Kapalı vergi döneminin verisi yerinde değiştirilemez; düzeltme kaydı gerekir.
- `TAX-INV-006`: Kural tarihi belge ekranının yerel saatine değil, şirket mali takvimine göre seçilir.
- `TAX-INV-007`: Resmi olarak doğrulanmamış oran veya alan canlı ortamda etkinleştirilemez.

## 7. API ve ekranlar

- `POST /api/v1/tax/determinations/preview`
- `POST /api/v1/tax/rules/{id}/publish`
- `GET /api/v1/tax/periods/{id}/workbook`
- `POST /api/v1/tax/periods/{id}/reconcile`
- `POST /api/v1/tax/declaration-exports`

Ekranlar: vergi kuralı editörü, iki tarihli değişiklik karşılaştırması, işlem vergi açıklaması, dönem çalışma alanı, uyum istisnaları ve kanıt deposu.

## 8. Yetkiler

- Vergi uzmanı: taslak kural ve çalışma dosyası.
- Vergi yöneticisi/mali müşavir: kural onayı ve dönem kapatma.
- Muhasebe: mutabakat ve düzeltme önerisi.
- Denetçi: salt okunur kural, dayanak ve hesap izleme.
- Sistem yöneticisi oran belirleyemez; sadece teknik erişim sağlar.

## 9. Test paketi

- Her resmi oran/kategori için alt, normal, iskonto, iade ve para birimi örneği.
- Yürürlük tarihinden bir saniye/gün önce ve sonra sınır testleri.
- Kural çakışması, boşluk ve geçmişe etkili değişiklik koruması.
- Satır/başlık yuvarlama ve kuruş farkları.
- Satış–muhasebe–KDV çalışma dosyası sıfır fark mutabakatı.
- Yetkisiz kural yayınlama ve kapalı dönem değiştirme saldırıları.
- Eski belgenin yeni oran sonrası aynı vergi anlık görüntüsünü koruması.

## 10. Canlıya geçiş kapısı

Vergi kodları/oranları, Tekdüzen hesap eşlemeleri, beyan alanları ve saklama süreleri [KKTC hukuki matrisindeki](../legal/01-kktc-legal-matrix.md) resmi dayanaklara bağlanıp yetkili mali müşavirce imzalanmadan üretim ortamına aktarılmaz. Gözlenen güncel oranlar yalnız araştırma girdisidir; bu doküman oran ilan etmez.

## 11. Vergi zamanı, kilit ve kontrol hesabı

TaxPointDate; invoice/document date, supply/delivery date, effective accounting date ve recorded time’dan ayrı, sürümlü TaxRule ile belirlenen alandır. Hangi tarihin geçerli olduğu KKTC resmi dayanak ve mali müşavir onayı olmadan kod varsayımı olamaz.

TaxLock, GL veya operasyon kilidinden ayrı scope’tur. Beyan kesimi kapandıktan sonra geç gelen belge için policy:

- eski dönemi resmi usulle reopen;
- sonraki açık dönemde original-period reference ile adjustment;
- manual/portal bildirimi

seçeneklerinden yazılı onaylı olanı uygular. Sistem sessizce tarihi değiştirip yeni döneme taşımaz.

TaxDecision; rule/version/source, tax point, base, rate/amount, exemption/reason, recoverability, rounding, account mapping ve input snapshot taşır. Vergi oranı kadar matrah, hesap sırası, charge/discount etkisi ve recoverable/nonrecoverable ayrımı da sürümlenir.

## 12. Vergi mutabakat paketi

Her beyan dönemi için:

- sales/purchase tax journals;
- taxable/exempt/zero/reverse/withholding benzeri onaylı sınıflar;
- document tax total ↔ tax subledger ↔ GL tax control account;
- geç/iptal/credit-debit correction;
- e-Fatura kabul/ret ve numara boşluğu;
- filed return snapshot, source file hash ve post-filing change

raporları aynı as-of/generation ile üretilir. Beyan gönderimi sonrası TaxDecision veya kaynak belge yerinde değişmez; correction zinciri ve filed-return impact görünür kalır.

Test veri seti yalnız oran örneği değil, tax-point sınırı, charge/discount sırası, yuvarlama, çok döviz, kısmi iade, late document ve lock/reopen senaryolarını kapsar.
