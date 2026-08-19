# ORG — Organizasyon ve Ortak Ana Veriler

## 1. Amaç

Tenant, şirket, şube, mali dönem, depo, konum, maliyet merkezi, iş takvimi, para/birim ve belge serisi gibi tüm modüllerin kullandığı organizasyon bağlamını yönetir.

## 2. Varlıklar

- `Tenant`: Bağımsız veri sınırı; immutable tenant code.
- `Company`: Yasal unvan, sicil/VKN, adres, fonksiyonel para, timezone, vergi profili.
- `Branch`: Resmi/operasyonel şube kodu, adres, e-Fatura şube kodu.
- `FiscalYear` ve `FiscalPeriod`: Tarih aralığı, open/soft-close/hard-close.
- `Warehouse` ve `Bin`: Depo hiyerarşisi ve sorumlusu.
- `CostCenter` / `ProfitCenter`: Tarih etkili boyut hiyerarşisi.
- `BusinessCalendar`: İş günü, resmi tatil, ödeme/vade hareket politikası.
- `Currency` ve `ExchangeRateType`: ISO referansı, functional/reporting kullanımı.
- `UnitOfMeasure` ve `UomConversion`: Boyut grubu ve dönüşüm.
- `DocumentType` / `DocumentSeries`: Şirket/şube/yıl bazlı numara politikası.
- `ReasonCode`: İptal, reversal, stock adjustment, override gibi standart gerekçe.

## 3. Şirket kurulum akışı

1. Tenant oluşturma platform admin ve ayrı operasyon kaydıyla.
2. Company yasal kimlik ve fonksiyonel para tanımı.
3. Branch ve Vergi Dairesi kayıt/şube eşlemesi.
4. Mali yıl/dönemler ve kapanış politikası.
5. Depo/bin ve maliyet merkezleri.
6. KKTC hesap planı/vergisel profil referansı.
7. Belge türleri ve seriler; e-Fatura özel numara profile.
8. Kullanıcı scope ve onay politikaları.
9. Açılış verisi gelmeden setup validation raporu.

## 4. Değişmezler

- Company functional currency ilk posted kayıt sonrası yerinde değişmez; yeni şirket/migration prosedürü gerekir.
- Fiscal period aralıkları aynı company'de çakışamaz.
- Branch e-Fatura kodu etki tarihi içinde unique.
- Warehouse başka company'ye taşınamaz; yeni depo ve transfer/migration.
- Kullanılmış UOM conversion geçmişe dönük değiştirilemez; yeni effective version.
- Document series kullanıldıktan sonra format değişimi ileri tarihli yeni version.
- Master `code` yeniden kullanılmaz; inactive kayıt kodu yeni varlığa verilemez.

## 5. Dönem durumu

| Durum | İzin |
|---|---|
| `open` | Normal posting |
| `soft_closed` | Özel permission + gerekçe; kapanış işlemleri |
| `review` | Yalnız kapanış batch/authorized adjustments |
| `hard_closed` | Posting yok; reopen gerekir |

Reopen belirli modül/tarih aralığıyla sınırlandırılabilir; süre sonunda otomatik kapanır.

## 6. Döviz kuru

- Kur kaynağı adapter ile; manuel kur taslak/onay.
- Rate key: currency pair + rate type + effective time.
- Direct ve inverse değer birbirinden türetilirken kullanılan hassasiyet saklanır.
- Aynı key için birden fazla published rate yok.
- Belge seçtiği kuru snapshot eder; sonraki rate update belgeyi değiştirmez.
- Eksik kur fail-closed; yetkili manual override ayrı permission/reason.

## 7. UOM

- Her item bir base UOM ve aynı dimension group içinde ek birimler.
- Dönüşüm `factor` decimal ve yönü açık.
- Fraction izin/ölçek item/UOM bazında.
- Stok movement base quantity; belge entered quantity/UOM da saklanır.
- Kullanılmış conversion düzeltmesi yeni version ve geleceğe etkili.

## 8. API

```text
GET/POST /api/v1/companies
GET/POST /api/v1/companies/{id}/branches
GET/POST /api/v1/companies/{id}/fiscal-periods
POST     /api/v1/fiscal-periods/{id}/soft-close
POST     /api/v1/fiscal-periods/{id}/hard-close
POST     /api/v1/fiscal-periods/{id}/reopen
GET/POST /api/v1/warehouses
GET/POST /api/v1/cost-centers
GET/POST /api/v1/exchange-rates
GET/POST /api/v1/document-series
```

## 9. UI

- Setup wizard yalnız başlangıç kolaylığıdır; her adım ayrı idempotent API use case.
- Company switcher kullanıcının scope'una göre; değişimde query cache ve sensitive view temizlenir.
- Dönem takvimi görsel durum, son close/reopen ve blocking checklist gösterir.
- Master değişikliğinde etkilenen açık belge sayısı ve ileri tarih seçimi.

## 10. Audit ve rapor

- Yasal kimlik, banka/vergi profili, period, series, currency/UOM ve hierarchy değişimi.
- Company setup completeness.
- Açık/review/kapalı dönem listesi ve reopen geçmişi.
- Belge sıra kullanım/gap raporu.
- Kapsamsız/inactive master kullanan draft belgeler.

## 11. Kabul kriterleri

- [ ] Çakışan fiscal period DB constraint ile reddediliyor.
- [ ] Posted veri sonrası functional currency değişmiyor.
- [ ] Branch/warehouse başka company scope'tan erişilemiyor.
- [ ] Kapanış ile yarışan posting deterministik blok/commit oluyor.
- [ ] Kur ve UOM geçmiş versiyonu eski belgeyi aynen yeniden hesaplıyor.
- [ ] Belge serisi paralel üretimde unique ve gap reason raporlu.

## 12. Yasal birim, operasyon birimi ve muhasebe boyutu

- Company yasal defter/vergi sahibi birimdir. Branch veya OperatingUnit ayrı tüzel kişi gibi bakiye üretmez; şirket altı belge, sorumluluk ve rapor boyutudur.
- Warehouse fiziksel facility’dir; maliyet merkezi veya şube ile aynı kimlik değildir. Bir facility birden çok location içerir.
- AccountingDimension ve DimensionValue tarih etkili hiyerarşidir; cost center, project, branch ve department gibi boyutlar journal line’a snapshot edilir.
- Company arası işlem iki ayrı yasal olay/defter üretir; yalnız tenant ortak diye tek fişle netleştirilmez. Intercompany ve konsolidasyon Faz 3 olsa da karşı şirket ve elimination referans alanları genişlemeye açıktır.

## 13. Dönem, kilit ve seri politikası

FiscalCalendar; mali dönem, vergi dönemi ve operasyon takvimini ayrı tanımlar. PeriodLock scope alanı operational, inventory, GL, tax veya hard değerlerinden biridir. Reopen başka scope’u açmaz.

DocumentSequence iki sınıftır:

- strict/legal: atanan numara geri verilmez; void/gap olayı ve yasal gerekçe gerekir;
- internal/gap-tolerant: benzersizlik ve sıra korunur fakat transaction rollback boşluğu iş hatası değildir.

Seri politikası company, branch, fiscal year, document type ve gerekirse channel bağlamında tarih etkili sürümlenir. Numara önizlemesi rezervasyon değildir; yalnız kesin issue transaction’ı numara tüketir.

Ana veri değişikliğinde eski belgenin yeniden üretimi için para, UOM, vergi, hesap eşleme, dimension ve party-role snapshot’ı korunur.
