# 01 — Ürün Kapsamı ve İlkeler

## Ürün vizyonu

Bir ticari olayın tekliften tahsilata, talepten ödemeye, fiziksel stoktan mali değere ve kaynak belgeden yasal kayda kadar aynı iz üzerinde yönetildiği KKTC odaklı ERP çekirdeği.

## Kullanıcı profili

- 50–250 çalışanlı şirket veya şirket grubu.
- Finans/muhasebe, satış, satın alma, depo, yönetim ve denetim ekipleri.
- Web ana iş istasyonu; Android sorgu, onay ve sınırlı saha işlemleri.
- Çoklu şirket, şube, depo ve döviz ihtiyacı.

## MVP kapsamı

- Kimlik, rol, permission, şirket/şube/depo/banka kapsamı.
- Şirket, dönem, belge serisi ve temel referans verileri.
- Müşteri/tedarikçi ve cari açık kalem/settlement/risk.
- Ürün/hizmet, depo, stok hareketi, rezervasyon, sayım ve temel maliyet.
- Satış teklif/sipariş/sevk/fatura/iade.
- Satın alma talep/onay/sipariş/mal kabul/fatura/ödeme hazırlığı.
- Banka, kasa, ekstre importu, ödeme/tahsilat ve mutabakat.
- Alınan/verilen çek ve senetlerin olay tabanlı takibi.
- KKTC Tekdüzen Hesap Planı, çift taraflı posting, kapanış ve temel mali tablolar.
- Tarih etkili KDV/vergi motoru; e-Fatura hazırlık, doğrulama, outbox ve arşiv.
- Audit, belge, bildirim, raporlama, web ve güvenli Android read-first uygulaması.
- Linux deployment, gözlem, yedekleme, PITR ve restore runbook.

## Faz 2

- Doğrudan KKTC e-Fatura entegrasyonunun resmi onay sonrası canlı kullanımı.
- Yazar kasa/POS sağlayıcı adaptörleri.
- Bütçe, sabit kıymet, gelişmiş maliyet ve BI modeli.
- Android sipariş/tahsilat taslağı ve kontrollü offline write.
- E-ticaret, tedarikçi portalı ve banka sağlayıcı API'leri.

## Faz 3

- Üretim/MRP, kalite, bakım, ileri WMS.
- CRM, bordro/İK, saha servis.
- Konsolidasyon ve ileri planlama.

## Non-goals

- Banka çekirdeği, ödeme kuruluşu veya hukuki karar sistemi olmak.
- Mali yazar kasa cihazını yazılım olarak taklit etmek.
- Türkiye'ye özgü GİB e-Dönüşüm varsayımlarını kopyalamak.
- Kesinleşmiş işlemi admin yetkisiyle sessizce değiştirmek.
- Tüm sektörlerin ilk sürümde karşılanması.

## Ürün ilkeleri

| ID | İlke | Sonuç |
|---|---|---|
| PROD-001 | Tek ticari gerçek | Aynı olay farklı modüllerde çelişkili bakiye yaratamaz |
| PROD-002 | Belge zinciri | Kaynak, dönüşüm, kalan ve karşı kayıt izlenebilir |
| PROD-003 | Önce doğruluk | Hız/kolaylık finansal invariantı aşamaz |
| PROD-004 | Yapılandırılabilir mevzuat | Tarih etkili kural ve kaynak snapshot'ı |
| PROD-005 | En az yetki | Kullanıcı yalnız gereken eylem ve kapsamı görür |
| PROD-006 | Açıklanabilir otomasyon | Sistem hangi kuralın neden seçildiğini gösterir |
| PROD-007 | Sade ama yoğun UX | Az dekorasyon; güçlü tablo, arama, klavye ve bağlam |
| PROD-008 | API tek kapı | Web, mobil ve entegrasyon aynı iş kuralını kullanır |
| PROD-009 | Restore edilebilirlik | Yedek değil geri dönüş kanıtı başarı ölçütüdür |
| PROD-010 | Küçük dikey dilimler | Her dilim veri + API + UI + test + audit ile tamamlanır |
| PROD-011 | Olay önce, fiş sonra | Sipariş/teslim/ödeme gibi ticari gerçek kaynak modeldir; GL onun açıklanabilir sonucudur |
| PROD-012 | Ayrı ama uzlaşan defterler | Stok, cari, banka, çek ve GL kaynak kimliği ile bağlanır; kontrol hesapları sıfır fark verir |
| PROD-013 | Zamanı doğru modelle | Etkin/yasal tarih, kayıt zamanı, posting zamanı ve banka valör tarihi aynı kavram değildir |
| PROD-014 | Kontrol kanıtı | Her kritik riskin sahibi, kontrolü, sıklığı, kanıtı ve exception süreci vardır |
| PROD-015 | Benimsenen sistem | Başarı go-live değil; doğru kullanım, kapanış kalitesi, eğitim ve ölçülen iş faydasıdır |

## Ürün başarı ölçütleri

- İki ardışık ay paralel kapanışta mizan, cari, stok, banka/kasa ve çek portföyü mutabık.
- Kesinleşmiş her fişte borç=alacak; kaynaksız GL satırı yok veya açıkça manual journal sınıfı.
- Kritik uçtan uca işlemler p95 hedeflerinde ve hatasız.
- Yetkisiz şirket verisi erişim negatif testlerinde sıfır sızıntı.
- e-Fatura test örnekleri güncel XSD/Schematron ve resmi senaryolardan geçiyor.
- Restore provası RPO/RTO içinde ve uygulama mutabakatı doğru.
- Taksitli/kısmi ödeme, allocation ve banka mutabakatı birbirini bozmadan yeniden üretilebilir.
- Geçmiş tarihli stok/mali olay sonrası etkilenen değerleme zinciri ve raporlar kontrollü repost ile mutabık.
- Sipariş–sevk–teslim–fatura satır bağlantılarında kısmi ve çoktan çoğa dönüşüm kayıpsız izlenir.
- Her kritik kontrolün yürütme kanıtı ve açık exception’ı dashboard/raporda görünür.
- Pilot sonrası 30/60/90 günlük kullanım, hata, kapanış süresi ve kullanıcı eğitim ölçüleri hedefleri karşılar.
