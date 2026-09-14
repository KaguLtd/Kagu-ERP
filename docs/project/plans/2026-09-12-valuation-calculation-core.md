# Stok değerleme hesap çekirdeği

- Amaç: Fatura maliyet girdisini normal pozitif stokta hareketli ortalamaya; seçilmiş çıkış maliyetini miktar ve değer değişimine dönüştürmek.
- Master fazı: MP-04; INV-COST-005/006; risk R3; durum validating.
- Sahip: Teknik uygulama; ürün/muhasebe atanmamış.
- Başlangıç: 12 Eylül 2026.
- Okunan belgeler: MASTER_PLAN.md, PLANS.md, docs/README.md, Inventory modülü, ortak iş akışları, test stratejisi, DEC-MP01-011.
- Ready: Normal pozitif stok hareketli ortalama, son bilinen/hiç geçmiş yoksa sıfır çıkış ve AwayFromZero kararı mevcut. Negatiften pozitif stoğa geçişte sonraki maliyet ve fark muhasebesi bu dilimin dışında.

## Kapsam ve sınırlar

Exact rational aritmetik ortaklaştırılır. Maliyet girdileri ve açık yuvarlama politika kimliği korunur. Açılış miktarı/değeri scoped watermark'a bağlanır; bu domain nesnesi authoritative DB kanıtı değildir. Normal giriş hesabı fiziksel stok kaydı yaratmaz; kaynak fatura toplamı, yuvarlanmış birim maliyet çarpımından yeniden üretilmez. Çıkış stok miktarını eksiye indirebilir; kalan değer sessiz sıfırlanmaz.

Yeni endpoint, permission, migration, GL posting, gerçek invoice producer veya runtime grant yoktur. Kişisel veri/log eklenmez. Mevcut posted kayıtlar değişmez. Geri dönüş yalnız yeni hesap çağrılarını kaldırmaktır; veri dönüşümü yoktur. Yetki, dönem, idempotency ve audit üretim orchestration katmanının sorumluluğunda kalır.

## Milestone ve done when

1. Exact bölme/çarpma, explicit ölçek ve typed overflow.
2. Scoped açılış bakiyesi ve normal alış sonrası ortalama; sıfır miktarda artık değer korunur/reddedilen özel durum açık kalır.
3. Seçilmiş çıkış maliyetinden signed miktar/değer sonucu; kaynak snapshot ve yuvarlama izi.
4. Unit negatifleri, miktar/değer invariant örnekleri, dar derleme, belgeler ve diff incelemesi.

5. Mevcut publication preview'dan exact satır tutarlarına aynı transaction içinde geçiş; nested savepoint ile miktar/tutar hesabı hatasında tüm iç audit'lerin telafisi.

## Doğrulama

Unit senaryoları normal ortalama, küsurat, negatif çıkış, geçmişsiz sıfır, scope/currency/cutoff uyuşmazlığı, taşma ve artık değeri kapsar. Runtime MP-04 toplu kapısında; dar compile kanıtı runtime başarısı değildir. DB/GL/API değişmediğinden yeni migration/endpoint testi yoktur; gelecekte kaynak→stok→GL golden ve gerçek DB position kilidi/idempotency kanıtı zorunludur. MP-04 kapısı ilerletilmez.

## İlerleme — 12 Eylül

Beş dilim uygulandı. Ortak exact rational aritmetik eski invoice unit-cost davranışını korur. Receipt hesabı kaynak toplamı korur; issue amount ve balance hesabı aynı fonksiyona dayanır. 2.525 bounded çift için receipt ve issue invariant senaryoları yazıldı. PostgreSQL harness'e publication→amount 37,04/0, politika eksiği, ikinci kaynak eksiği, audit hatası ve ikinci satır tutar taşmasında önceki audit'lerin rollback senaryoları eklendi. Runtime sonuçları henüz yoktur.

İlk dar derlemelerde CA1512 yardımcı kontrolü ve CA1806 negatif fixture uyarısı düzeltildi; analizörler kapatılmadı. Son ek negatiflerden sonra `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj -c Release --no-restore -v:q` 2,81 sn ve `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release --no-restore -v:q` (Bootstrap dahil) 5,47 sn: sıfır uyarı/hata. `git diff --check` içerik hatası yok; önceki lock dosyalarında yalnız CRLF→LF uyarıları var. Commit/push yok.

Negatif açılış sonrası sonraki birim maliyet/fark yöntemi için ürün sahibine örnekli nonblocking soru gönderildi; yanıt gelmeden bu özel durumun politikası kodlanmadı. Gerçek invoice producer, opening balance authority, period gate ve stock/GL atomik writer sonraki işlerdir. API/OpenAPI runtime CodeIntegrity engeli ve MP-04 mali golden kanıt borcu korunur.
