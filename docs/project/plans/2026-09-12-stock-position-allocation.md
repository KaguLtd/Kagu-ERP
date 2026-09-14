# Sevk için transaction bağlı stok pozisyonu hazırlama

- MP-04 / INV-COST-007, SALES-DSP-008; R3; durum validating.
- Amaç: Maliyet önizlemesindeki hareket sequence ve recorded-time değerlerini istemci yerine kilitli DB pozisyonundan hazırlamak.
- Okunanlar: MASTER_PLAN MP-04, AGENTS, docs/README, Inventory, ortak iş akışları, test stratejisi, 0043 unique position, position-lock protokolü ve dispatch-cost planı.
- Ready: Position advisory lock, forced scope, immutable stock movement ve warehouse authority mevcut; negatif stok maliyet farkı kararı bu teknik dilimi bloklamaz.
- Sahip: Teknik Codex, ürün/muhasebe atanmadı; 12 Eylül 2026.

## Kapsam / done when

1. 1–500 unique line kimliği + stok pozisyonu + maliyet watermark'ı doğrulanır. Cost view ve bütün depoların actor kapsamı zorunludur.
2. Tüm pozisyonlar mevcut canonical sırada kilitlenir. Effective date içindeki max persisted sequence ve aynı gündeki maliyet watermark floor üstünden deterministic line sırası hazırlanır; bigint taşması fail-closed. DB clock timestamp kaynağıdır.
3. Sevk composition rezervasyon demand/position kilitlerini önce alır, ardından allocation ve maliyet okumasını aynı outer transaction'da birleştirir. SQL veya kaynak hatasında bütün audit geri alınır.
4. Gerçek DB harness'e max/floor, tekrar, kapsam ve unchanged stock senaryoları eklenir; dar derleme ve belge güncellemesi.

Bu bir sequence rezervasyon tablosu veya posting/idempotency receipt değildir. Kilit bırakıldıktan sonra aday yeniden kullanılmaz; tekrar preview aynı sequence üretebilir. Writer aynı transaction içinde persist etmelidir ve DB uniqueness son savunmadır. Başka writer'lar lock protokolüne katılmadan global concurrency kanıtı iddia edilmez. Backdate/period izni, kur/policy seçimi, last-known producer, stock/GL writer kapsam dışıdır. Yeni migration/grant/API yok; kişisel veri/log yok; rollback yeni çağrıyı kaldırmaktır. Runtime ve finansal/authorization DB kanıtı MP-04 kapanışında; compile tamamlanma değildir. Commit/push yok.

## 12 Eylül ilerleme

Inventory allocator ve Bootstrap LoadAllocatedAsync eklendi. Mevcut gerçek DB fixture senaryoları persisted max/watermark floor, iki satır unique sıra, DB kayıt zamanı, ters input sırasında aynı adaylar, long.MaxValue taşmasında bütün audit rollback ve permission eksiğini kapsayacak şekilde genişletildi. Fixture maliyeti sentetiktir; gerçek fatura producer sayılmaz. Final Integration Release (Bootstrap dahil) derlemesi 5,87 sn, sıfır uyarı/hata; diff check içerik hatası yok, eski lock dosyalarında CRLF/LF uyarıları var. Runtime/DB testi MP-04 kadansına ertelendi; özellikle concurrency, RLS ve finansal doğruluk kanıtı henüz yok. MP-04 kapısı değiştirilmedi. 13 Eylül devamında rounding authority bağlantısı ayrı yaşayan plana taşındı.
