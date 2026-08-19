# KKTC Mevzuat ve Teknik Uyum Matrisi

## 1. Kullanım ve sınır

Bu belge yazılım gereksinimlerine dönüştürülmüş araştırma matrisidir; hukuki veya mali müşavir görüşü değildir. Kesim tarihi **19 Ağustos 2026**. Her madde, canlıya geçmeden yetkili KKTC mali müşaviri/hukukçu ve gerektiğinde ilgili kurumla doğrulanmalıdır.

Durumlar:

- `araştırma-doğrulandı`: resmi kaynak incelendi, teknik tasarım girdisi çıkarıldı.
- `uzman-onayı-bekliyor`: mevzuat yorumu veya firma özelinde karar gerekiyor.
- `kurum-onayı-bekliyor`: resmi başvuru/test/ruhsat gerekiyor.
- `uygulanacak`: karar kapandıktan sonra testli gereksinim.

## 2. Uyum matrisi

| Alan | Resmi kaynak / gözlem | Teknik gereksinim | Kanıt / test | Sahip | Durum |
|---|---|---|---|---|---|
| Tekdüzen hesap planı | Gelir ve Vergi Dairesi “KKTC Tekdüzen Hesap Planı” yayını | Hesap planını sürümlü resmi kaynaktan içe aktar; yerel hesap eşlemelerini koddan ayır | Kaynak hash/sürüm, hesap ağacı ve kayıt testleri | Mali müşavir + GL | uzman-onayı-bekliyor |
| Muhasebe yazılımı/programcı yetkisi | Dairenin yetkili bilgisayar programcıları sayfası | Yazılımın/programcının kayıt, inceleme veya onay gereğini canlı öncesi netleştir | Yazılı kurum cevabı/başvuru belgesi | Ürün sahibi | kurum-onayı-bekliyor |
| E-fatura kapsamı | 26.06.2026 birleştirilmiş kurallar; 2025 satış/gayrisafi iş hasılatı ≥ 2 milyar TL için 01.01.2027 zorunluluk | Firma eşiğini hesapla; zorunlu/gönüllü geçiş kararını konfigüre et | Mali veriye göre imzalı kapsam analizi | Vergi uzmanı | araştırma-doğrulandı |
| E-fatura değişmezliği | Düzenlenen e-faturada değişiklik yapılmaması | Kabul edilen payload/hash değişmez; iptal/düzeltme ayrı olay ve ters kayıt | Mutation engeli, hash ve audit testi | EINV | araştırma-doğrulandı |
| E-fatura numarası | VKN 9 + yıl 4 + şube en çok 9 + sıra 11; yinelenen reddedilir | Atomik şirket/yıl/şube seri tahsisi, unique constraint | Parallel allocation ve format testi | EINV | araştırma-doğrulandı |
| Doğrudan entegrasyon | Başvuru, test ve Daire onayı | Adaptör feature flag'i onay olmadan prod'da kapalı; portal fallback | Resmi test/onay ve prod config kanıtı | Ürün + EINV | kurum-onayı-bekliyor |
| Yurt dışından yönetilen IT sistemi | Daire onayı gereksinimi | Hosting/operasyon/veri konumu mimari dosyada; gerekli onay alınmadan dış yönetim yok | Topology/veri akışı ve kurum cevabı | Hukuk + Ops | kurum-onayı-bekliyor |
| E-fatura iptali | Resmi uygulama, ilgili KDV beyan süresi; iki taraf kullanıcıysa alıcı onayı ihtimali | İptal durum makinesi, dönem kontrolü, alıcı bekleme durumu, kanıt arşivi | Takvim sınır/alıcı onay E2E | EINV + Vergi | araştırma-doğrulandı |
| E-fatura arşivi | Bütünlük, okunabilirlik, DB/depolama/görüntüleme imkânı | Payload + görsel + cevap + şema/hash; WORM hedefi, restore/açma testi | Üç aylık restore/hash/görüntü örneği | Ops + EINV | araştırma-doğrulandı |
| E-fatura veri kaybı/bozulması | Üç iş günü içinde bildirim ve tamamlama planı ihtiyacı | Incident yasal saat sayacı, etki raporu, restore/mutabakat ve yetkili bildirim adımı | DR tatbikatı ve bildirim paketi | Incident komutanı | araştırma-doğrulandı |
| E-fatura teknik format | Dairenin yardım dosyaları/kılavuzları | UBL-KKTC, XSD/Schematron/örnekler sürümlü fixture ve mapper | Resmi sandbox/sertifikasyon paketi | EINV | kurum-onayı-bekliyor |
| KDV oran/kural/beyan | Daire mevzuatı/duyuruları ve mali müşavir yorumu | Oran/istisna/beyan alanlarını yürürlük tarihli kural; hard-code yok | Her sürüm resmi kaynak + golden vergi testi | Vergi uzmanı | uzman-onayı-bekliyor |
| Yasal defter/çıktı/saklama | Vergi ve muhasebe mevzuatı, firma türü | Yevmiye/defter/rapor biçimi, numara ve saklama süresini resmi karara bağla | Çıktı örneği ve mali müşavir imzası | GL + Hukuk | uzman-onayı-bekliyor |
| Kişisel verilerin korunması | KKTC Kişisel Verileri Koruma Kurulu mevzuat sayfası | Veri envanteri, amaç/dayanak, erişim, saklama, talep/olay süreci | DPIA/ROPA benzeri kayıt ve erişim testleri | Veri sorumlusu | uzman-onayı-bekliyor |
| Yurt dışına kişisel veri transferi | Kurulun veri transfer ruhsatı duyurusu; başvuruların 09.09.2024'ten itibaren alınması | Bulut yedek, e-posta, push, analytics, destek ve repo veri akışını sınıflandır; gerekli ruhsat/sözleşme olmadan transferi kapat | Veri akış diyagramı, sağlayıcı ülkesi, ruhsat/karar | Veri sorumlusu | kurum-onayı-bekliyor |
| Çek işlemleri | KKTC Merkez Bankası çek mevzuatı dizini | Çek kimliği, ciro, ibraz/karşılıksız ve saklama akışını bankalar/hukukla doğrula | Hukuki süreç matrisi ve banka örnekleri | Finans + Hukuk | uzman-onayı-bekliyor |
| Banka ödeme/ekstre | Banka sözleşmesi/teknik formatı | Banka başına adaptör, maker-checker, idempotency ve mutabakat | Banka sandbox/dosya örneği | Finans + INT | kurum-onayı-bekliyor |
| Yazar kasa/POS | Gelir ve Vergi Dairesi güncel düzenleme ve cihaz sağlayıcı protokolü | Cihaz entegrasyonu ayrı adaptör; günlük toplam ve fatura çifte kayıt kontrolü | Resmi protokol ve saha cihaz testi | Satış + Vergi | açık |
| Belge saklama/silme | Vergi, ticaret, kişisel veri ve olası dava yükümlülükleri birlikte | Belge sınıfına göre sürümlü retention + legal hold; tek genel süre yok | Retention matrisi ve silme dry-run | Hukuk + DOC | uzman-onayı-bekliyor |
| Vergiyi doğuran olay/tax point | Resmi KKTC vergi düzenlemesi ve mali müşavir yorumu gerekir | Fatura, teslim, ödeme ve kayıt tarihlerini ayır; tax-point kuralını yürürlük tarihli yönet | Sınır tarih, geç belge ve iade senaryoları | Vergi uzmanı | uzman-onayı-bekliyor |
| Beyan sonrası geç belge/düzeltme | Resmi beyan ve düzeltme usulü doğrulanmalı | Tax lock sonrası reopen mı sonraki dönemde adjustment mı uygulanacağını policy yap; sessiz tarih değiştirme yok | Filed-return change ve correction-period testi | Vergi + GL | uzman-onayı-bekliyor |
| Stok değerleme ve cut-off | Firma politikası ile KKTC muhasebe/vergi kabulü doğrulanmalı | Sürekli/dönemsel yöntem, GRNI, yoldaki mal ve geçmiş tarihli değerleme/repost kuralını onaylat | Stok–GL ve dönem sonu çalışma dosyası | Mali müşavir + INV | uzman-onayı-bekliyor |
| Banka ekstre delil/formatı | Banka sözleşmesi ve örnek dosya/API | Booking/value date, statement sequence ve opening/closing kontrol toplamını koru; format profilini bankayla onayla | camt/MT940/CSV fixture ve kapanış mutabakatı | Finans + INT | kurum-onayı-bekliyor |

## 3. E-fatura teknik kontrol listesi

- [ ] Firma zorunluluk/gönüllülük statüsü yazılı hesaplandı.
- [ ] Resmi kural ve teknik paket sürüm/hash'i saklandı.
- [ ] VKN/yıl/şube/sıra numara biçimi resmi örneklerle doğrulandı.
- [ ] Portal mı doğrudan entegrasyon mu kararı ve Daire onayı var.
- [ ] Yurt dışı IT yönetimi/hosting kararı ve gerekiyorsa onay var.
- [ ] Test/üretim credential ve endpoint ayrıldı.
- [ ] XSD/Schematron ve vergi/toplam mutabakatı geçiyor.
- [ ] Sonucu bilinmeyen gönderimde durum sorgusu var.
- [ ] İptal süresi/alıcı onayı akışı resmi testte doğrulandı.
- [ ] Arşiv okunabilirlik/hash/restore tatbikatı var.
- [ ] Üç iş günlük incident prosedürü ve sorumlular var.

## 4. Tekdüzen hesap planı ne anlama gelir?

Tekdüzen hesap planı, firmaların muhasebe hareketlerini ortak numara ve başlık düzeninde sınıflandırmasıdır. Örneğin kasa, banka, müşteri, stok, gelir ve giderler belirlenmiş hesap gruplarında izlenir. ERP açısından bu, her satış/stok/banka olayının onaylı hesaplara dengeli fiş üretmesi demektir.

Uygulama hesap numaralarını kaynak koda sabitlemez: resmi plan içe aktarılır, firmanın alt hesapları ve kayıt şablonları yetkili mali müşavir onayıyla sürümlenir. Böylece plan değiştiğinde geçmiş fişler bozulmadan yeni tarihten itibaren yeni eşleme uygulanır.

## 5. KKTC'ye özel uygulama bileşenleri — kısa özet

- KKTC Tekdüzen Hesap Planı ve yerel kayıt şablonları.
- Yürürlük tarihli KKTC KDV/vergi kuralları ve beyan çalışma alanı.
- KKTC e-fatura UBL/numara/gönderim/iptal/arşiv ve resmi onay adaptörü.
- Üç iş günlük e-fatura veri olayı runbook'u.
- KKTC kişisel veri ve yurt dışı transfer ruhsatı kontrolü.
- Çek, banka ve yazar kasa akışlarının yerel mevzuat/sağlayıcı adaptörleri.
- Yerel iş günü, saat dilimi, mali dönem ve Türkçe belge/rapor biçimleri.

## 6. Değişiklik izleme

Hukuk/vergiden sorumlu kişi en az aylık ve release öncesi Daire/Kurul/Merkez Bankası duyurularını kontrol eder. Yeni kaynak:

1. indirilen içerik/URL/tarih/hash ile kanıt deposuna alınır,
2. önceki sürümle farkı çıkarılır,
3. iş, veri, arşiv ve geriye dönük etki analizi yapılır,
4. kural sürümü ve test fixture'ı hazırlanır,
5. iki kişi/uzman onayıyla yürürlük tarihinde yayınlanır,
6. eski belgelerin anlık görüntüsü korunur.

Resmi site erişilemezse önbellek/dosyaya dayanarak sessiz mevzuat güncellemesi yapılmaz; son doğrulama tarihi kullanıcıya/operasyona görünür olur.

## 7. Teknik araştırmanın hukuki sınırı

OASIS UBL, ISO 20022, COSO ve yabancı/açık kaynak ERP davranışları teknik tasarım ve iç kontrol karşılaştırmasıdır; KKTC mevzuat kaynağı değildir. Örneğin başka bir ERP’nin vergi kilidi veya düzeltme dönemi uygulaması KKTC’de izin verildiğini kanıtlamaz.

Bu kaynaklardan alınanlar yalnız güvenli ayrımlardır: kaynak belge–defter izi, ödeme–allocation–banka mutabakatı, tarih alanlarının ayrılması, değişmez kayıt, kontrol hesabı ve kanıt. Oran, tax-point, beyan dönemi, düzeltme, resmi belge, saklama ve hesap eşlemesi yalnız resmi KKTC kaynağı/uzman onayıyla publish edilir.
