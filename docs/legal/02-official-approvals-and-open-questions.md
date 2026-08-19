# Resmi Onaylar ve Açık Sorular

## 1. Amaç

Kodla çözülemeyen mevzuat, kurum, banka ve firma politikası kararlarını tek kapıda izler. Her soru için yazılı cevap/kanıt, karar sahibi, son tarih ve etkilediği gereksinim bulunmalıdır.

## 1.1 MP-01 blocking/non-blocking sınıflandırması

`Blocking`, tüm geliştirmeyi durdurmak anlamına gelmez; tablodaki hedef fazın, feature'ın veya production kullanımının güvenle ilerleyemediğini belirtir. Fake adapter, sentetik veri, versioned contract ve kapalı feature flag gibi geri döndürülebilir teknik işler `non-blocking` sütununda gösterilmiştir.

| Soru grubu | Blocking hedef | Blokaj nedeni | Non-blocking güvenli çalışma | Sahip rol |
|---|---|---|---|---|
| Gelir ve Vergi Dairesi 1–5, 12–15 | `feature:EINV`, `feature:TAX`, production | Zorunluluk, profil, numara, iptal, tax point ve düzeltme usulü resmi kuraldır | Tarih etkili rule/profile modeli, fake/portal adapter, XSD test harness; gerçek oran/endpoint kapalı | Ürün + vergi uzmanı + EINV |
| Gelir ve Vergi Dairesi 6–10 | production; ilgili cevap gerekiyorsa deployment | Dış yönetim, programcı/yazılım onayı, arşiv ve yasal çıktı resmî kanıt ister | Local development, sentetik veri, sürümlü archive/export contract | Ürün + hukuk + operasyon + GL |
| Gelir ve Vergi Dairesi 11 | `feature:POS`, production | Cihaz protokolü ve günlük mutabakat bilinmiyor | POS adapter portu; gerçek cihaz bağlantısı yok | Satış + vergi + entegrasyon |
| Kişisel Verileri Koruma Kurulu/hukukçu 1–8 | production ve kişisel veri taşıyan dış servisler | Amaç/dayanak, saklama, transfer ve olay yükümlülüğü belirsiz | Sentetik/local veri, dış telemetry/push/backup kapalı, veri minimizasyonu iskeleti | Güvenlik/veri sorumlusu + hukuk |
| Yetkili mali müşavir 1–9, 12–15 | MP-03 ve ilgili mali feature'lar | Hesap planı, posting, dönem, allocation, rapor ve kontrol hesabı beklenenleri tanımsız | Accounting kernel spike, decimal/invariant, parametrik rule contract; gerçek hesap/kural yok | Yetkili mali müşavir/muhasip |
| Yetkili mali müşavir 10–11 | MP-04/MP-05 stok ve satın alma | Değerleme, backdate, GRNI ve cut-off mali anlamı belirsiz | Generic stock ledger/impact preview ve matching contract | Muhasebe + stok/satın alma sahibi |
| Bankalar ve finans 1–10 | Banka entegrasyonu ve production ödeme/mutabakat | Format, kimlik, status query ve banka kanıtı sağlayıcıya özgüdür | Kanonik statement/payment modeli, fake provider, sentetik fixture | Finans + güvenlik + entegrasyon |
| Firma içi ürün kararları: tenant/company/scope/rol/para/dönem | MP-03; repository sınırı için MP-02 | Veri sahipliği, yetki ve mali bağlam bilinmeden gerçek davranış güvenli değildir | Generic çok-company/RLS/auth spike; production-ready kabul edilmez | Ürün + teknik + güvenlik + muhasebe |
| Firma içi ürün kararları: stok/satın alma/çek/banka | İlgili MP-04/MP-05 feature | İş ve kontrol politikaları state/invariant sonucunu değiştirir | Parametrik domain contract ve sentetik test fixture | Ürün + muhasebe + süreç sahibi |
| Firma içi ürün kararları: RPO/RTO/hosting/backup/on-call | MP-02 çıkış kapısı ve production | Restore hedefi, veri konumu ve operasyon sorumluluğu bilinmiyor | Local backup/restore smoke; uzak hedef veya gerçek veri yok | Operasyon + güvenlik + ürün |
| Firma içi ürün kararları: Android/MDM | MP-08 ve production mobil | Cihaz desteği, dağıtım ve kayıp cihaz riski bilinmiyor | Android klasör/toolchain iskeleti; gerçek kullanıcı verisi yok | Ürün + güvenlik + mobil |

MP-01 firma kararlarının tekil kimlik, durum ve faz etkisi [MP-01 karar kaydında](../project/04-mp01-decision-register.md) tutulur. Bu dosya resmi/uzman sorularının kanıt kapısı olmaya devam eder.

## 2. Gelir ve Vergi Dairesine sorulacaklar

1. Firma cirosu/faaliyeti/şirketleri için e-fatura zorunluluk başlangıcı ve gönüllü geçiş şartı nedir?
2. Portal kullanım ile doğrudan entegrasyon başvuru/test/sertifikasyon adımları ve süreleri nelerdir?
3. Güncel UBL-KKTC, XSD, Schematron, kod listesi, test endpoint ve örnek olumlu/olumsuz belgelerin kesin sürümü nedir?
4. Fatura numarasındaki şube alanı, sıra sıfır doldurma, yıl dönümü, boşluk/iptal ve farklı belge tipleri kuralları nedir?
5. İptal/alıcı onayı/KDV beyan süresi ve düzeltme belgesi senaryolarının resmi API/portal akışı nedir?
6. Yurt dışından yönetilen bilgi işlem sistemi tanımı; Linux sunucu KKTC'de, uzaktan geliştirici/destek yurt dışındaysa gereken onay nedir?
7. E-fatura arşivinin zorunlu saklama süresi, imza/zaman damgası, görüntü formatı ve denetimde sunum şekli nedir?
8. Üç iş günlük veri kaybı/bozulması bildiriminin formu, alıcısı, içeriği ve iş günü takvimi nedir?
9. Tekdüzen muhasebe yazılımı için programcı/yazılım kayıt-yetki-onay süreci, teknik inceleme ve yenileme yükümlülüğü nedir?
10. Yasal defter/beyan/export için kabul edilen format, numaralama ve elektronik saklama kuralları nelerdir?
11. Yazar kasa/POS/fatura birleşiminde gereken cihaz protokolü ve günlük mutabakat kuralları nedir?
12. Vergiyi doğuran olayda fatura, mal/hizmet teslimi, tahsilat ve düzenleme tarihinden hangisi hangi senaryoda esas alınır?
13. Beyanı kapanmış döneme ait geç gelen fatura, credit/debit note veya iptal hangi dönemde ve hangi resmi düzeltme usulüyle kaydedilir?
14. Vergi ve yasal defter kilitleri için resmi “yeniden açma” veya sonraki dönem adjustment sınırları nelerdir?
15. UBL satırlarının sipariş/sevk/teslim referansları, charge/allowance ve prepayment gösterimi için KKTC profilinin zorunlu/yasak alanları nelerdir?

## 3. Kişisel Verileri Koruma Kuruluna / hukukçuya

1. ERP şirketinin veri sorumlusu/veri işleyen rolleri ve kayıt/bildirim gereksinimleri.
2. Çalışan, müşteri, tedarikçi, kullanıcı logu ve mobil cihaz verilerinin hukuki dayanağı/saklama süresi.
3. Yurt dışı Git/CI, crash analytics, push, e-posta, object backup, support erişimi için hangi transfer ruhsatı/sözleşme gerekir?
4. Şifreli fakat anahtarı firmada olan yurt dışı yedek aktarım sayılır mı ve şartları nedir?
5. Veri sahibi erişim/düzeltme/silme talebi ile mali/yasal saklama çakışması nasıl uygulanır?
6. Olay bildirimi eşikleri, süre, içerik ve alıcılar.
7. Log/audit/IP/cihaz parmak izi için minimizasyon ve saklama.
8. Android push mesajında hangi veri bulunabilir?

## 4. Yetkili mali müşavire

1. KKTC Tekdüzen Hesap Planı sürümü, firma alt hesapları ve boyutlar.
2. Satış, satın alma, stok, banka, kasa, çek/senet, avans, iskonto, iade, kur farkı ve masraf dağıtımı fiş şablonları.
3. Stok değerleme yöntemi ve dönem sonu maliyet düzeltmesi.
4. Güncel KDV oranları/kategorileri, istisna kodları, vergiyi doğuran olay ve yuvarlama.
5. Beyan çalışma dosyası alanları, dönem devri ve düzeltme.
6. Açık belge mi bakiye mi taşınacağı; açılış fişi ve kaynak sistem mutabakatı.
7. Dönem kapanış, yeniden açma, ters fiş ve manuel fiş onay politikası.
8. Mali rapor düzeni, karşılaştırma para birimi ve imza/onay.
9. Belge/defter/kanıt saklama süreleri ve denetimde sunum.
10. Sürekli veya dönemsel stok değerleme; FIFO/hareketli ortalama ve geçmiş tarihli hareketin kabul edilen düzeltmesi.
11. Mal kabul–tedarikçi faturası zaman farkında GRNI/tahakkuk, yoldaki mal ve teslim edilmiş–faturalanmamış satış kayıtları.
12. Payment kaydı, açık kalem allocation’ı ve banka mutabakatının fiş tetikleyicileri; transit/outstanding hesapları.
13. Taksit başına açık kalem, avans/fazla ödeme, write-off ve kur farkı hesapları.
14. Cash-flow statement/workpaper ve retained earnings/year-end closing biçimi.
15. Kontrol hesapları için cari, stok, banka ve çek alt defter mutabakatının para/boyut/as-of yöntemi.

## 5. Bankalara ve finans ekibine

1. Ekstre formatı, encoding/tarih/ondalık, benzersiz işlem referansı ve geçmiş erişim.
2. Ödeme dosyası/API formatı, imza/sertifika, idempotency ve durum sorgusu.
3. “Gönderildi ama yanıt yok” halinde güvenli referans/mutabakat prosedürü.
4. Yeni alıcı, çift onay, limit ve banka tarafı yetkilendirme.
5. Çek seri/şube, tahsile verme, ciro, teminat ve karşılıksız sonucu verisi.
6. Test/sandbox, sertifika rotasyonu, bakım/SLA ve destek kanalı.
7. ISO 20022 camt.052/053/054, MT940, OFX veya banka CSV seçeneklerinden hangisi; kesin mesaj/profil/namespace sürümü nedir?
8. Ekstre opening/closing/available balance, statement sequence, booking date ve value date alanlarının anlamı ve kontrol toplamı nedir?
9. Tek banka satırının çok ödemeye veya çok banka satırının tek ödemeye eşleştiği senaryoların banka referans kuralı nedir?
10. İade/chargeback, banka masrafı, faiz ve “gönderildi ama işlenmedi” olaylarında kesin durum sorgusu ve muhasebe kanıtı nedir?

## 6. Firma içi ürün kararları

- Grup altında kaç tüzel şirket; veri ortak mı ayrı mı?
- Şube/depo/proje/masraf merkezi kapsamı.
- Kullanıcı/rol ve tutar limitleri; görevler ayrılığı.
- Negatif stok, rezervasyon, lot/seri ve maliyet politikası.
- Kredi/risk, vade, kur kaynağı ve kur farkı.
- Satın alma toleransı ve siparişsiz işlem istisnası.
- Çek ciro sonrası risk politikası.
- Finansal rapor/KPI tanımları.
- Android rol ve cihaz parkı; MDM/dağıtım.
- RPO/RTO, bakım penceresi ve on-call bütçesi.
- Hosting ve uzak yedek ülkesi/sağlayıcısı.
- Eski Logo tarihçesinin ne kadarının transactional taşınacağı.
- Ödeme ile allocation ve bank reconciliation’ın hangi rollerce, hangi tarihte ve hangi toleransla kesinleştirileceği.
- Satışta gelir/stock cut-off tetikleyicisi; satın almada 2/3/4 yönlü eşleştirme politikası.
- Kör sayım, recount eşiği ve sayım sırasındaki hareketlerin çözümü.
- GL, tax, inventory ve hard lock kapsamları ile reopen quorum’u.
- Rapor kataloğu, as-of/currency ve source-to-GL drill-down kabulü.
- Kontrol kataloğundaki her riskin owner, sıklık, kanıt ve exception SLA’sı.

## 7. Kanıt kaydı biçimi

Her cevap:

```yaml
decision_id: LEG-000
question: "..."
authority: "Kurum / uzman / şirket yetkilisi"
request_date: YYYY-MM-DD
response_date: YYYY-MM-DD
source_url_or_file: "..."
content_sha256: "..."
decision: "..."
effective_from: YYYY-MM-DD
affected_requirements: [EINV-..., TAX-...]
owner: "..."
review_due: YYYY-MM-DD
```

E-posta/sözlü cevap tek başına kod değişikliği için yeterli değilse resmi yazı veya uzman imzalı karar istenir. Kişisel veri içeren kanıt erişim kontrollü saklanır.

## 8. Üretim no-go maddeleri

Aşağıdakiler açıkken ilgili özellik üretimde açılmaz:

- e-fatura doğrudan entegrasyon resmi test/onayı yok,
- yurt dışı veri/IT yönetimi için gereken izin yok,
- vergi oran/kural ve hesap eşlemesi mali müşavirce onaylı değil,
- ödeme banka credential/çift onay/mutabakat testi yok,
- yedek şifreleme anahtarı ve restore tatbikatı yok,
- kişisel veri envanteri/erişim/saklama/transfer kararı yok,
- kaynak–hedef mali migration farkı açıklanmamış,
- kritik/yüksek güvenlik açığı veya şirketler arası erişim testi başarısız.

Portal/manual ve salt okunur mod gibi güvenli sınırlı seçenekler, açıkça belgelenip yetkili onayıyla kullanılabilir.

## 9. Araştırma kaynaklı karar kayıtları

Yabancı/açık kaynak ERP veya ders kitabından gelen öneri için kanıt kaydı ayrıca:

- observed_behavior;
- source URL/version/date;
- KKTC’ye doğrudan uygulanamaz hukuki kısımlar;
- accepted design invariant veya rejected pattern;
- muhasebe/hukuk/teknik sahipleri;
- test ve yeniden değerlendirme tetikleyicisi

alanlarını taşır. Bu kayıt resmi kurum/uzman cevabı yerine geçmez; yalnız neden belirli bir güvenlik veya veri modeli kararının alındığını açıklar.
