# ADR-0006 — Kesinleşmiş Finansal Kayıtlar Append-only

- Durum: Accepted — bütünlük değişmezi
- Tarih: 2026-08-18
- Son doğrulama: 2026-08-19 — ERPNext immutable ledger ve accounting-pattern karşılaştırması

## Bağlam

Fatura, ödeme, stok/cari hareket, çek ve muhasebe fişinin yerinde güncellenmesi denetim izini ve dönem/mutabakat doğruluğunu bozar. Hata düzeltme gereği yine vardır.

## Karar

Taslak düzenlenebilir. “Kesinleşmiş/postalanmış” sonuç ve satırlar UPDATE/DELETE edilemez. Düzeltme kaynak kayda bağlanan iptal, ters olay/fiş veya karşı belgeyle yapılır. Durum olayları ve audit append-only tutulur. Güncel görünüm projeksiyon olabilir; kaynak olayların yerine geçmez.

## Sonuçlar

- Tam iz, yeniden üretilebilir bakiye ve güvenilir denetim.
- Kullanıcı düzeltme akışları daha dikkatli tasarlanır.
- Veri büyür; indeks/partition/archive planı gerekir.
- GDPR/KVKK silme talepleri mali saklama yükümlülüğüyle hukukça yönetilir; finansal kanıt sessiz silinmez, gerekirse erişim/anonimleştirme uygulanır.

## Reddedilenler

- Admin SQL ile fiş düzeltme.
- Soft-delete'i mali düzeltme saymak.
- Geçmiş fişi yeni kural/oranla yeniden hesaplayıp üzerine yazmak.

## Kanıt

DB yetki/trigger/constraint veya domain guard; mutation negatif testi; ters kayıt bağlantısı; mizan/alt defter property ve restore sonrası mutabakat testi.

## Kaynak gerçek, allocation ve projection ayrımı

Append-only kuralı şu katmanlarda farklı uygulanır:

- Business economic event: hata business reversal/correction/return/credit note ile düzelir.
- Subledger entry: kaynak olaya bağlı hareket değişmez; karşı hareket eklenir.
- Payment allocation: payment ve open-item arasındaki bağ ayrı allocation/unallocation event’idir; nakit/GL hareketini silmez.
- Bank reconciliation: immutable statement line ile internal movement arasındaki onaylı bağ karşı kararla düzeltilir.
- GL: booked journal reverse entry ile düzelir.
- Read model/cache: türetilmiştir; versioned projection generation ile rebuild edilebilir.

## Repost sınırı

Repost geçmiş ekonomik gerçeği düzeltme yolu değildir. Yalnız doğru kaynak olaydan üretilmiş bozuk/eski projection’ı kontrollü olarak yeniden kurar. Zorunlu koşullar:

1. source scope/checksum ve eski generation dondurulur;
2. dry-run old/new satır, toplam, rule ve closed-period/tax impact gösterir;
3. yetki ve materiality’ye göre farklı kişi onayı;
4. yeni generation staging’de borç=alacak ve subledger/control-account mutabakatı;
5. atomik switch, eski generation audit ve before/after evidence;
6. dış outbox/e-Fatura/payment side effect’i üretilmez.

Kaynak belge yanlışsa repost reddedilir; düzeltme domain correction/reversal ile başlar.

## Zaman ve kanıt

Her hareket document/effective date ile recorded/posted time’ı ayrı taşır. Backdate, önceki satırı overwrite etmez; dönem/vergi/stok değerleme etkisini ve gerekirse sonraki projection range’ini hesaplar.

Ek kanıtlar: allocation + unallocation net sıfır; aynı source/rule rebuild checksum eşit; tek aktif posting generation; kaynak→subledger→GL→report drill-down; restore sonrası aynı control totals.
