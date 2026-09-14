# Kalıcı sevk → maliyet önizleme bağlantısı

- Amaç: Sevk taslağının gerçek satırlarını rezervasyon ve maliyet tutarlarıyla aynı transaction içinde birleştirmek.
- Faz/requirement/risk: MP-04, SALES-DSP-008 / INV-COST-006, R3.
- Durum: validating; sahip ürün/muhasebe atanmadı, teknik uygulama Codex.
- Okunanlar: AGENTS.md, MASTER_PLAN MP-04, docs/README, Sales sevk, Inventory maliyet, ortak iş akışları, test stratejisi ve önceki değerleme planı.
- Ready: Immutable draft, current order/reservation kontrolü ve published cost okuyucusu mevcut. Negatif stoktan dönüş fark politikası bu bağlantının bağımlılığı değildir.

## Tasarım ve done when

1. Bootstrap mapper draft satırları ile explicit movement ID/sequence/watermark/currency bağlamını exact order-line kümesinde eşler. Ürün/depo/UOM/miktar/effective date draft'tan gelir; eksik/fazla/duplicate veya yanlış scope reddedilir. Immutable draft kaynak sürümü 1'dir; order version draft'ta ayrıca korunur.
2. Transaction katılımcısı mevcut current-order/reservation preview'ı, mapper'ı ve amount preview'ı aynı outer savepoint içinde çalıştırır. Her hata bütün iç audit'leri geri alır; caller commit sahibidir.
3. Negatif scope/cutoff/kimlik testleri ve dar derleme, belge güncellemesi.

İç hazırlık servisidir; HTTP/DI/grant açılmaz. Position sequence ve recorded time production allocator'dan, currency/rounding policy authoritative profile'dan sağlanmalıdır; ham API isteği kabul edilmez. Watermark en güncel/final maliyet kanıtı değildir; gerçek publication producer hâlâ eksiktir. Posted dispatch, reservation consume, stok/GL, kapalı dönem/backdate ve fatura producer kapsam dışıdır. Yeni migration yok; geri dönüş yeni çağrıyı kaldırmaktır. Yetki/audit mevcut katılımcılarda korunur; miktar/tutar loglanmaz. MP kapısı değişmez.

## Doğrulama

Mapper için kaynak miktar/kimlik/tarih, line-set tamlığı, duplicate hareket/pozisyon, scope/UOM/currency ve cutoff negatifleri yazılır. Gerçek DB zinciri ve tüm audit rollback kanıtı MP-04 kapanış paketinde bekler; compile runtime kanıtı değildir. Mevcut güvenlik politikası bypass edilmez, commit/push yapılmaz.

## 12 Eylül uygulama kaydı

Mapper ve outer-savepoint composition eklendi. Mevcut integration fixture içinde gerçek draft + reservation + test cost publication üzerinden iki satırda 37,04 TRY sonucu; eksik yayın/yetki/eşleme durumunda audit geri alımı; unchanged reservation lifecycle ve sıfır stock movement senaryoları yazıldı. Synthetic cost publication gerçek satınalma faturası producer kanıtı değildir. Kaynak satırların yeniden sıralanması, eksik/duplicate kimlik, ortak hareket ID/pozisyon, başka şirket, geçmiş kayıt zamanı ve mixed currency negatifleri hazırlandı.

`dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release --no-restore -v:q` Bootstrap dahil son mixed-currency/stock-write negatiflerinden sonra 8,00 sn, sıfır uyarı/hata. `git diff --check` içerik hatası yok; eski lock dosyalarında CRLF/LF uyarıları var. Runtime/gerçek DB testleri kullanıcının MP-sonu kadansı ve mevcut CodeIntegrity engeli nedeniyle çalıştırılmadı; authorization ve finansal sonuç runtime doğrulanmış sayılmaz. Yeni migration/API/permission grant yok, commit/push yapılmadı. Sonraki iş: authoritative stock position/currency/rounding provider ve gerçek maliyet producer; ardından posted dispatch/consume/GL zinciri.
