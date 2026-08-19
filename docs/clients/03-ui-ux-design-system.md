# UI/UX ve Tasarım Sistemi

## 1. Tasarım yönü

Öneri: **shadcn/ui'nin sakin, açık ve sahip olunabilir bileşen yaklaşımı; yoğun ERP iş akışlarına uygun daha kontrollü veri tabloları ve belge yüzeyleriyle birleştirilsin.** Hazır temanın birebir kopyası değil, repo içinde sahip olunan token ve bileşenlerden oluşan bir sistem kurulmalıdır.

Karakter:

- nötr, ciddi ve düşük görsel gürültülü,
- birincil eylemi belirgin, diğerlerini sakin,
- veri yoğun ama sıkışık olmayan,
- kart duvarı yerine hiyerarşi, boşluk ve ince sınırlarla ayrılan,
- gradient, glassmorphism, büyük gölge ve dekoratif animasyondan uzak,
- finansal doğruluk ve durum görünürlüğünü öne alan.

## 2. UX ilkeleri

1. **Bağlam kaybolmaz:** aktif şirket, şube, dönem ve para birimi görünürdür.
2. **Durum anlaşılır:** taslak/onayda/kesinleşmiş/iptal gibi durumlar eylemleri açıklar.
3. **Geri alınamaz işlem yavaşlatılır:** sonuç özeti, gerekçe ve gerektiğinde yeniden kimlik doğrulama.
4. **Sık işlem hızlandırılır:** klavye, komut arama, kayıtlı filtre, akıllı varsayılan.
5. **Hata çözüm sunar:** ne oldu, hangi satır, neden, ne yapılmalı, korelasyon kodu.
6. **Toplamın kaynağı bulunur:** dashboard → rapor → fiş → iş belgesi zinciri.
7. **Yetki görünür ama güvenlik sunucudadır:** kullanıcının yapamadığı şey ve nedeni anlaşılır.
8. **Web ve mobil aynı dili konuşur:** token/terim/durum ortak, etkileşim platforma özgüdür.

## 3. Görsel tokenlar

Tokenlar CSS değişkenleri ve mobil tema tanımlarıyla tek sözlükten türetilir.

### 3.1 Renk

- Nötr yüzey: sıcak olmayan slate/zinc ekseni.
- Birincil vurgu: güven veren koyu mavi/indigo; marka kararıyla kesinleşir.
- Başarı, uyarı, hata, bilgi semantik tokenları; ham renk sınıfı feature kodunda kullanılmaz.
- Muhasebede borç/alacak yalnız kırmızı/yeşil yapılmaz; başlık, işaret ve hizalama bulunur.
- Açık tema ilk sürüm zorunlu; koyu tema semantik kontrast ve tablo testleri tamamlanınca etkinleşir.

Örnek isimler: `--background`, `--surface`, `--surface-subtle`, `--border`, `--text`, `--text-muted`, `--primary`, `--success`, `--warning`, `--danger`, `--focus-ring`.

### 3.2 Tipografi

- Web: Inter veya Geist Sans; self-hosted/font lisansı ve Türkçe glif kontrolü.
- Gövde 14–16 px; yoğun tabloda erişilebilir kompakt ölçü.
- Sayılar `font-variant-numeric: tabular-nums` ve sağ hizalı.
- Belge numarası/kod için gerektiğinde mono yardımcı stil; uzun metin mono değildir.
- Başlık ölçeği sınırlı; ERP ekranında dev pazarlama başlıkları kullanılmaz.

### 3.3 Boşluk ve şekil

- 4 px taban aralığı; yaygın 8/12/16/24/32.
- Radius 6–10 px aralığı; her kutuyu aşırı yuvarlama yok.
- Gölge yalnız katman/modal/popover ayrımı için.
- Dokunma hedefi mobilde en az erişilebilir platform ölçüsündedir; web kompakt modda dahi klavye odağı nettir.

## 4. Uygulama kabuğu

### Web

- Sol daraltılabilir ana navigasyon: modül grupları.
- Üst çubuk: şirket/şube/dönem seçici, global arama/komut, görev, bildirim, profil.
- İçerik başlığı: sayfa adı, bağlam, durum ve en fazla bir belirgin ana eylem.
- Breadcrumb yalnız gerçek hiyerarşi olduğunda.
- `Cmd/Ctrl+K`: sayfa, cari, ürün, belge ve komut arama; sonuç yetki filtreli.

### Android

- En sık 3–5 görev için alt navigasyon; geri kalan modül menüsü/arama.
- Telefon list→detay; tablet list-detail iki panel.
- Platform geri davranışı ve sistem barları doğal çalışır.
- Webdeki yoğun tablo küçük ekrana sıkıştırılmaz; özet satır/kart + ayrıntı kullanılır.

## 5. Temel bileşen kataloğu

shadcn tabanı: Button, Input, Select, Combobox, Checkbox, Radio, Dialog, Sheet, Popover, Dropdown, Tabs, Tooltip, Toast, Alert, Calendar, Command.

ERP bileşenleri:

- `CompanyContextSwitch`
- `MoneyInput` / `MoneyDisplay`
- `QuantityInput` / `UnitDisplay`
- `TaxBreakdown`
- `StatusBadge` + metin açıklaması
- `DocumentNumber`
- `DocumentShell`
- `LineItemGrid`
- `DataGrid`
- `FilterBar` / `SavedView`
- `ApprovalTimeline`
- `AuditTimeline`
- `ReconciliationWorkspace`
- `AccountPicker`, `PartyPicker`, `ItemPicker`
- `AsyncJobStatus`
- `EmptyState`, `ErrorState`, `PermissionState`, `ConflictState`

Her bileşenin Storybook benzeri kataloğunda durumları, klavye davranışı, erişilebilir adı, responsive görünümü ve tasarım tokenı belgelenir.

## 6. Veri tablosu standardı

- Tablo varsayılan yoğunluğu “rahat kompakt”; kullanıcı tercihi saklanabilir.
- En önemli kimlik ve durum solda, tutar sağda, eylemler en sağda.
- Satır tıklaması ile checkbox seçimi karıştırılmaz.
- Hücrede kritik içerik yalnız tooltip arkasına gizlenmez.
- Filtreler görünür chip olarak özetlenir; “tüm filtreleri temizle” vardır.
- Toplam, yalnız yüklenen sayfanın mı tüm sonucun mu olduğu açıkça yazılır.
- Kolon kişiselleştirme finansal anlamı bozan zorunlu kolonları saklayamaz.
- Yatay kaydırmada kimlik ve ana eylem mümkünse sabit kalır.

## 7. Form ve belge standardı

- Uzun form; “kimlik, ticari koşullar, satırlar, vergi/toplam, ekler” gibi anlamlı bölümlere ayrılır.
- Zorunlu alan yalnız yıldızla değil açıklama ve hata ile belirtilir.
- Hata özeti üstte, odak ilk hataya gider; veri kaybolmaz.
- Belge toplamı sağ/alt sabit özet olabilir; küçük ekranda doğal akışa geçer.
- Sonuçlandırma öncesi değişecek stok/cari/muhasebe etkisi gösterilir.
- Kesinleşmiş belge düzenleme formu gibi görünmez; salt okunur zaman çizelgesi ve ters/düzelt eylemi sunar.
- Tehlikeli onay diyalogu eylem, hedef, tutar, geri alma yöntemi ve gerekçeyi açıklar; “Emin misiniz?” tek başına yeterli değildir.

## 8. Durum, geri bildirim ve boş ekran

- Skeleton yalnız yapıyı koruyorsa; sonsuz spinner yok.
- Uzun işlerde kuyruk durumu, ilerleme/son güncelleme ve ayrılabilme gösterilir.
- Toast geçici teyit içindir; kritik sonuç sayfada kalıcıdır.
- Boş ekran “veri yok” yanında neden ve izinli sonraki eylemi söyler.
- Yetkisiz ekran 404/403 bilgi sızıntısı politikasına uygun mesaj verir.
- Çakışma ekranı eski/yeni değer ve yenile/kopyala/yeniden gönder seçeneği sunar.

## 9. Erişilebilirlik

Hedef WCAG 2.2 AA:

- semantik HTML ve doğru başlık sırası,
- tüm etkileşimde klavye ve görünür odak,
- 200% zoom ve reflow,
- metin/arayüz kontrastı,
- error/label/description programatik ilişkisi,
- hareket azaltma tercihi,
- durumun renk dışı ifadesi,
- tablo başlık ve caption ilişkileri,
- ekran okuyucu canlı bölge kullanımının sınırlı/doğru olması.

Otomasyon erişilebilirlik kanıtının yalnız bir parçasıdır; klavye, NVDA/VoiceOver/TalkBack ve düşük görüş manuel senaryoları gerekir.

## 10. Tasarım teslim ve yönetişim

- Figma şart değildir; kod tabanlı bileşen kataloğu ana gerçek kaynaktır. Figma kullanılırsa token/bileşen isimleri birebir eşlenir.
- Yeni varyant eklemeden mevcut bileşen kompozisyonu değerlendirilir.
- Ham renk/spacing değeri feature içinde kullanılmaz; lint/design review engeller.
- Bileşen değişikliği finans, satış ve mobil örnek ekranlarda regresyon kontrolü ister.
- UX metinleri Türkçe terminoloji sözlüğüne bağlıdır.
- Kullanıcı araştırması: muhasebeci, depo, satış, yönetici ve mobil saha rolünden en az birer temsilciyle görev testi.

## 11. İlk tasarım doğrulama paketi

Kod başlamadan düşük/orta doğrulukta şu akışlar prototiplenir:

1. satış faturası hazırlama ve sonuçlandırma,
2. stok hareketi/lot seçimi,
3. banka ekstre eşleştirme,
4. satın alma fatura istisnası,
5. mobil ödeme onayı,
6. mizan → fiş → kaynak belge drill-down,
7. yetkisiz/çatışmalı/hatalı durum.

Başarı ölçüleri: görevi tamamlama, kritik hata, süre, yardım ihtiyacı ve kullanıcı güveni. Bulgular gereksinim/ADR olarak belgelenir.

## 12. Muhasebe bilgi mimarisi

shadcn/ui sadeliği korunur; ancak ERP yoğunluğu “az bilgi” değil, iyi hiyerarşi demektir. Detail shell şu sıralamayı kullanır:

1. kimlik, şirket/şube ve ana tutar;
2. ayrı business/accounting/settlement/bank/integration status strip;
3. izinli next actions;
4. satırlar ve adjustment/tax toplamları;
5. source/target document links;
6. açık kalem/allocation veya stok/GL etkisi;
7. approval, audit ve ekler.

Durum yalnız renkle anlatılmaz; kısa metin ve ikon birlikte kullanılır. “Posted”, “bankada bekliyor”, “kısmen tahsisli” ve “e-Fatura reddedildi” aynı yeşil başarı rengine indirgenmez.

## 13. Yeni bileşen kalıpları

- StatusStrip: birbirinden bağımsız durum eksenleri.
- SourceLineage: sipariş/sevk/fatura/payment/journal bağlantı grafiği.
- AccountingImpact: preview/actual borç-alacak, currency, dimension ve rule.
- AllocationEditor: ödeme ve vade kalemlerini split/merge eder; kalanları sürekli cross-foot eder.
- ReconciliationWorkbench: statement line, aday, skor nedeni, fark ve approval.
- AsOfBanner: report timestamp, watermark, generation ve stale uyarısı.
- ControlEvidence: control owner, son çalışma, sonuç, kanıt ve exception.
- IrreversibleActionDialog: etki, numara/dönem, düzeltme yolu ve gerekçe.

Bu bileşenler Radix/shadcn primitive’lerinden proje içinde sahip olunan kodla üretilir; üçüncü taraf ERP ekranı görsel olarak kopyalanmaz.

## 14. Finansal erişilebilirlik ve hata önleme

- Debit/credit, incoming/outgoing ve positive/negative anlamı yalnız işaret veya renge bırakılmaz.
- Para kolonunda currency görünür; locale formatı parse edilen ham değeri değiştirmez.
- Büyük tablolarda sticky total ile seçili/filtreli/genel toplam ayrılır.
- Kör sayım expected quantity’yi erişilebilirlik ağacında dahi expose etmez.
- Error mesajı rule, neden, çözüm, correlation ve izinli next action’ı açıklar; teknik stack yoktur.
- Klavye kullanıcıları grid edit, satır seçimi, toplam, validation ve action’a mantıklı sırayla ulaşır.

Kullanılabilirlik testi yalnız “ekran güzel mi” değil; kısmi sevk, allocation, mutabakat, posting exception ve dönem kapanışı gibi hata riski yüksek görevlerde ölçülür.
