# Web Uygulaması Geliştirme Şartnamesi

## 1. Amaç ve teknoloji kararı

Orta ölçekli firma personelinin günlük ERP işlemlerini hızlı, erişilebilir ve denetlenebilir biçimde yapacağı masaüstü öncelikli web istemcisidir.

Önerilen taban:

- React + TypeScript (`strict`),
- Vite,
- shadcn/ui yaklaşımı + Tailwind CSS,
- TanStack Query (sunucu durumu),
- React Hook Form + Zod (form ve istemci doğrulaması),
- TanStack Table/Virtual (yoğun tablolar),
- React Router,
- OpenAPI'den üretilen tipli API istemcisi,
- Vitest + React Testing Library + Playwright + axe.

Tam sürümler repo oluşturulurken güncel kararlı sürümlere sabitlenir; bağımlılık güncelleme politikası [bakım dokümanında](../operations/04-maintenance-and-upgrades.md) uygulanır.

## 2. Güven sınırı ve oturum

- Tarayıcı PostgreSQL'e veya iç servislere doğrudan bağlanmaz.
- Üretimde tek origin: `https://erp.example.com`; web statikleri ve `/api` ters proxy arkasındadır.
- Tercih edilen oturum modeli güvenli BFF/sunucu oturumu veya `HttpOnly`, `Secure`, uygun `SameSite` cookie'dir.
- Access/refresh token `localStorage` veya `sessionStorage` içinde tutulmaz.
- Durum değiştiren cookie tabanlı isteklerde CSRF koruması uygulanır.
- OIDC giriş/çıkış, MFA ve yeniden doğrulama Keycloak üzerinden yapılır.
- UI'da buton gizlemek güvenlik değildir; her API çağrısı sunucuda yetkilendirilir.

## 3. Dizin yapısı

```text
apps/web/src/
├── app/                 # bootstrap, router, providers, error boundary
├── routes/              # route tanımları ve sayfa kompozisyonu
├── features/
│   ├── sales/
│   ├── inventory/
│   ├── purchasing/
│   ├── finance/
│   └── accounting/
├── entities/            # paylaşılan iş varlığı sunum modelleri
├── components/
│   ├── ui/              # shadcn taban bileşenleri; kontrollü özelleştirme
│   └── erp/             # Money, StatusBadge, DataGrid, DocumentShell...
├── api/
│   ├── generated/       # elle değiştirilmez
│   └── adapters/
├── lib/                 # auth, format, validation, telemetry
├── styles/              # token ve global stiller
├── test/                # fixture, mock server, yardımcılar
└── main.tsx
```

Bir feature başka feature'ın iç dosyasını import etmez; kamuya açık `index.ts` veya ortak domain sunum modeli kullanır. `components/ui` jeneratör çıktısını kontrol altında tutar; iş mantığı içermez.

## 4. Veri yönetimi

- Sunucu durumu TanStack Query cache'indedir; Redux benzeri global store yalnız gerçek istemci durumu gerekirse ADR ile eklenir.
- Query key her zaman şirket/şube bağlamını ve filtreyi içerir.
- Şirket değiştirilince ilgili cache temizlenir veya kapsam anahtarı değişir.
- Mutasyon sonrası tüm cache'i silmek yerine hedefli invalidation yapılır.
- İyimser güncelleme finansal postalamada kullanılmaz; düşük riskli tercihlerde düşünülebilir.
- Liste filtre/sıralama/sayfalama sunucu taraflıdır; URL'de paylaşılabilir durum tutulur.
- Tüm tarihler API'de ISO 8601; kullanıcıya şirket saat dilimi ve yerel biçimde gösterilir.

## 5. Form ilkeleri

- Sunucu doğrulaması yetkilidir; istemci doğrulaması hızlı geri bildirim içindir.
- Para için kayan nokta kullanılmaz; API decimal'i string veya tanımlı money sözleşmesiyle taşır.
- Form taslağı otomatik kaydedilecekse açıkça gösterilir; kesinleşmiş belgeye otomatik yazılmaz.
- Kaydedilmemiş değişiklikte rota/sekme kapatma uyarısı verilir.
- `409` sürüm çatışmasında kullanıcının sürümü ile güncel sürüm karşılaştırılır; sessiz üzerine yazma yoktur.
- İşlem başarısında yalnız toast değil, sayfa durumu ve belge zaman çizelgesi güncellenir.
- Sunucu hatası alan/iş kuralı/genel hata olarak ayrılır; korelasyon kodu gösterilir.

## 6. ERP tabloları

Standart `DataGrid` şu yeteneklere sahiptir:

- sunucu sayfalama/sıralama/filtre,
- sütun görünürlüğü ve kullanıcı tercihi,
- sabit başlık ve uygun sütun sabitleme,
- klavye gezinmesi ve görünür odak,
- sayı/para için sağ hizalama ve tabular rakam,
- toplamların kapsam ve para birimi etiketi,
- satır durumunun renk + metin/ikonla gösterimi,
- izinli dışa aktarma,
- sanallaştırmada erişilebilirlik ve odak testi.

Toplu işlem yalnız aynı şirket, durum ve yetki bağlamındaki kayıtlarda açılır. Kullanıcı işlemden önce etkilenen kayıt sayısını ve sonuç özetini görür.

## 7. Sayfa kalıpları

- **Liste:** başlık, kayıt sayısı, hızlı arama, filtre çubuğu, veri tablosu, izinli ana eylem.
- **Belge detayı:** kimlik/durum, ana eylemler, özet, satırlar, finansal toplam, ilişkiler, ekler, zaman çizelgesi.
- **Çalışma masası:** istisna/iş kuyruğu, filtrelenmiş aday, sağ ayrıntı paneli.
- **Rapor:** filtre, kesim zamanı, metrik/grafik, ayrıntı tablo, dışa aktarma.
- **Ayar:** sürümlü yapılandırma, yayınlama öncesi fark ve etki analizi.

## 8. Performans bütçesi

- Normal bağlantıda oturum sonrası ana kabuk etkileşime hazır p75 ≤ 2,5 s hedeflenir.
- Feature rotaları lazy-load edilir; muhasebe kodu satış ekranına başlangıçta taşınmaz.
- İlk route JavaScript bütçesi ölçülür ve CI'da regresyon eşiği konur.
- Büyük tablo tüm satırları tarayıcıya çekmez.
- Arama debounce ve istek iptali kullanır.
- Görsel/ikonlar boyutlandırılır; gereksiz animasyon/kütüphane yoktur.

Gerçek eşikler [performans dokümanındaki](../quality/03-performance-and-capacity.md) test donanımıyla doğrulanır.

## 9. Erişilebilirlik ve yerelleştirme

- Hedef WCAG 2.2 AA.
- Tüm işlevler klavye ile erişilir; görünür odak vardır.
- Form label, açıklama ve hata ilişkileri semantiktir.
- Modal açıldığında odak yönetilir ve kapatılınca kaynağa döner.
- Renk tek başına durum belirtmez; kontrast token testleri yapılır.
- Türkçe ilk dil; metinler kod içinde parçalı birleştirilmez, i18n anahtarı kullanılır.
- Para/tarih/sayı `Intl` ile, şirket para birimi ve saat dilimine göre biçimlenir.
- Kullanıcı girdisi gösterilirken XSS güvenli render; zorunlu HTML sanitization ile.

## 10. Hata, telemetri ve gizlilik

- Global error boundary kurtarma seçeneği ve korelasyon kodu gösterir.
- Beklenen 4xx iş hataları crash telemetrisi değildir.
- Web vitals, route süresi, API hata/latans ve kritik işlem sonucu ölçülür.
- Telemetride cari adı, fatura içeriği, VKN, banka bilgisi, token veya dosya bulunmaz.
- Source map üretimde yalnız yetkili hata platformuna yüklenir; genel sunulmaz.

## 11. Test stratejisi

- Saf fonksiyon/bileşen: Vitest.
- Kullanıcı davranışı ve erişilebilir sorgu: Testing Library.
- API mock: sözleşmeden türetilmiş MSW fixture'ları.
- Kritik akış: Playwright gerçek tarayıcı + test API/veritabanı.
- Otomatik erişilebilirlik: axe; ayrıca klavye/ekran okuyucu manuel turu.
- Görsel regresyon: tasarım sistemi ve kritik belge sayfalarında kontrollü.

Zorunlu E2E: giriş/şirket seçimi, satış siparişi→fatura, satın alma→kabul, banka mutabakatı, onay, mizan drill-down ve yetkisiz erişim.

## 12. Web için tamamlanmış sayılma

- OpenAPI istemcisi güncel ve elle yamalanmamış.
- Loading/empty/error/forbidden/conflict durumları tasarlanmış.
- Klavye ve responsive testleri geçmiş.
- Hassas veri/token depolama incelemesi geçmiş.
- Feature testleri ve kritik Playwright akışları yeşil.
- Bundle/performance bütçesi aşılmamış.
- Yetki ve audit davranışı API ile birlikte doğrulanmış.

## 13. ERP çalışma masaları

Liste/detail CRUD ekranı yanında role göre iş tamamlama masaları tasarlanır:

- Accounting Exceptions: source event, posting error, rule, owner ve retry/correct/repost seçenekleri.
- Bank Reconciliation: statement line, internal adaylar, skor nedenleri, split/merge ve maker-checker.
- Period Close: görev, control owner, mutabakat sonucu, kanıt, exception ve lock.
- Inventory Count: plan, blind count, concurrent movements, variance ve recount.
- AR/AP Allocation: payment/credit, vade kalemleri, kur/fark ve kalan.

Workbench satır eylemi yalnız mevcut business/accounting/lock durumunda ve server’dan gelen allowedActions ile sunulur. UI gizleme authorization sayılmaz.

## 14. Çoklu durum ve accounting impact görünümü

Belge detail’de tek status badge yerine ayrı başlıklar kullanılır:

- İş süreci;
- Muhasebe;
- Kapama/allocation;
- Banka;
- e-Fatura/entegrasyon.

Timeline kaynak taahhüt → ekonomik olay → alt defter → GL → allocation/reconciliation bağlarını tarih ve actor ile gösterir. “Muhasebe etkisini önizle” alanı post öncesi hesap, borç/alacak, vergi, currency/rate, dimension ve kural versiyonunu; post sonrası gerçek journal linkini gösterir. Yetkisiz maliyet/hesap alanı sadece CSS ile gizlenmez, API’den gelmez.

Geri döndürülemez eylemlerde generic “Emin misiniz?” yerine belge, dönem, toplam, etki, numara tüketimi ve düzeltme yöntemini açıklar. Reversal, unallocation ve repost farklı metin/permission/impact preview taşır.

## 15. Rapor ve stale-data UX

Rapor ekranı as-of zamanı, data-through watermark, projection generation, currency/rate ve aktif filtreleri görünür tutar. Drill-down aynı bağlamı korur. Concurrent posting sonrası pagination token stale ise satırları sessizce karıştırmak yerine yenileme çağrısı gösterilir.

Export öncesi row count, control totals, kapsam ve hassas veri uyarısı; sonrası file hash/audit reference sunulur. Karşılaştırmalı raporda null, sıfır ve veri henüz yüklenmedi durumları ayrıdır.

Playwright akışları kısmi allocation, bankada bekleyen ödeme, posting exception retry, close checklist, backdate impact ve source-to-GL drill-down’ı kapsar.
