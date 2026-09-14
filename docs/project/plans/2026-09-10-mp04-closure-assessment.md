# MP-04 kapanış değerlendirmesi

- **Amaç:** Kullanıcının MP-04'ü bitirme isteğini gerçek master çıkış kapılarına bağlamak; rezervasyon alt işini fazın tamamı olarak sunmamak.
- **Faz/risk:** MP-04 / R4.
- **Durum:** in-progress; kapanış için politika kararları, eksik uygulama ve runtime doğrulama gerekiyor.
- **Sahip:** Ürün/muhasebe atanmadı; teknik Codex.
- **Tarih:** 10 Eylül 2026.
- **Okunan kaynaklar:** MASTER_PLAN MP-04 teslimatlar/çıkış kapısı, karar kaydı DEC-MP01-011/013/014/025, Inventory maliyet/sayım, Sales sevk/fatura/iade, rezervasyon ve sevk hazırlama yaşayan planları, repo kod envanteri.
- **Ready:** Miktar rezervasyonu DEC-025 kapsamındadır. Değerleme/backdate/sayım mali sonuçları DEC-011 açıkken seçilemez. Bu belge yeni politika onayı değildir.

## Kapanış kapsamı ve mevcut kanıt

| İş paketi | Mevcut durum | Kapanışta eksik |
|---|---|---|
| Sipariş lifecycle ve miktar rezervasyonu | Domain, SQL/writer, atomic confirm/cancel/release, receipt ve HTTP routing kodları var | MP-04 runtime/DB/concurrency/authorization kanıtı; gerçek gateway ve sınırlı grant henüz kapalı |
| Sevk | Read-only hazırlık, miktar evidence ve 0051 immutable draft persistence kodu var; runtime bekliyor | Posted dispatch/allocation, reservation consume, stok çıkışı, maliyet/GL bağlantısı ve kısmi sevk |
| Stok değerleme ve dönem etkisi | Miktar/impact-preview; last-known/zero history publication; exact invoice unit cost/normal MWA/issue amount; kaynak mutabakatı ve receipt doğrulaması; toplu maliyet/fingerprint kodları var, runtime bekliyor | Gerçek invoice source producer, authoritative opening balance ve maliyet publication producer, değerleme writer'ı, kalan DEC-011/backdate/repost ve cut-off kanıtı |
| Satış faturası | Şartname sözleşmesi var; Sales kod envanterinde uygulama yok | Fiyat/vergi snapshot, source-line bağları, cari açık kalem ve gelir/KDV posting'i |
| İade | Şartname sözleşmesi var; Sales kod envanterinde uygulama yok | Orijinale bağlı miktar, stok disposition ve cari/GL/vergi karşı kayıtları |
| Web operasyon akışı ve rapor | Bu stok/satış zincirinin tamamlandığına dair kanıt yok | Sipariş→sevk→fatura→iade kullanımı, exception ve drill-down |
| Ortak kapı | MP-04 test kodları hazırlanmış; faz runtime kanıtı yok | Miktar korunumu, stok değeri–GL uzlaşması, concurrency, backdate ve kapanış testleri |

MP-04 completed değildir. Yalnız derleme ve yeni dosya sayısıyla bir tamamlanma yüzdesi üretilmez.
MP-03 teknik temel kullanılabilir fakat kullanıcı golden UAT kabulü ayrıca açıktır.

14 Eylül durum netleştirmesi: Purchasing Contracts ve Infrastructure capture writer katmanları
mevcut; 0053 ilk fatura taslağı persistence migration'ı yazıldı fakat henüz uygulanmadı.
Finalized invoice lifecycle veya gerçek SQL cost producer yoktur. Son oturumlardaki doğrulama,
mutabakat, readonly preview ve fingerprint çalışmaları bu eksikliği kapatmış sayılmaz. Sonraki
esas uygulama paketi bağımsız fatura/kabul kaynak kayıtları ve atomik kapasite kullanımından
başlayıp authoritative maliyet yayınına, ardından posted dispatch/consume/stock/GL transaction'ına
ilerlemelidir. Yeni küçük doğrulama sayıları MP-04 bitiş yüzdesi veya kapanış kanıtı değildir.

## Kullanıcı kararı bekleyen dar paket — DEC-MP01-011

**10 Eylül güncellemesi:** Kullanıcı aşağıdaki ilk taslağın 1. maddesini kabul etti, 2–4'ü değiştirdi:
hareketli ortalama onaylı; eksi stok miktarı serbest; kesinleşmiş düzeltme yöneticiye ait;
sayım farkı fişini yönetici veya yetkili kullanıcı işleyebilir. Güncel karar kaydı ve Inventory modülü
güncellendi. Aşağıdaki liste tarihsel taslaktır, özellikle eksi stok yasağı ve zorunlu ikinci onay
artık uygulanacak politika değildir. Sonraki kullanıcı cevabıyla eksiye çıkan sevkte son bilinen
maliyetin kullanılması da onaylandı. 12 Eylül sonraki kararla hiç maliyet geçmişi olmayan
ürünün çıkışı sıfır maliyetle ilerler; manuel başlangıç maliyeti zorunlu değildir.

Aşağıdakiler yalnız onaya sunulan taslaktır; uygulanmış/onaylanmış politika değildir:

1. **Maliyet:** Şartnamedeki MVP hareketli ağırlıklı ortalama. Yeni mal girişleri ortalama birim maliyeti günceller; çıkış o andaki maliyet snapshot'ını kullanır. FIFO sonraki faza bırakılır.
2. **Eksi stok:** İlk sürümde kullanılabilir miktarı aşan sevk/çıkış reddedilir; yönetici dahil gizli bypass yoktur.
3. **Geri tarih/düzeltme:** Kapalı döneme yazılmaz. Açık dönemde geçmiş tarihli stok işlemi, etki analizi ve yetkili onayı sonrası kontrollü değerleme yeniden hesaplamasıyla yapılır; kesinleşmiş kayıtlar yerinde değiştirilmez. Onay isteyen kritik işlemde hazırlayan ile onaylayan farklı kişiler olur.
4. **Sayım farkı:** Sayımı hazırlayan kişi kendi farkını kesinleştiremez; farklı yetkili kişinin onayıyla ayrı stok ve muhasebe düzeltme hareketi oluşturulur. Fark oranı/tutarına dayalı otomatik bypass ilk sürümde yoktur.

DEC-013 resmi vergi kuralı ve DEC-014 resmi e-Fatura sözleşmesi onaylanmış sayılmaz. Sentetik test
vergi snapshot'ı gerçek mevzuat onayı değildir. Resmi üretim entegrasyonu MP-06 sınırında kalır;
MP-04 teknik fatura/posting senaryosunun bu sınırla ilişkisi uygulama planında açık tutulmalıdır.

## Ortam engeli

Windows CodeIntegrity/Operational 3077 kayıtları üretilen .NET DLL yüklemesinin imza/kod bütünlüğü
politikasınca engellendiğini doğrular. OpenAPI/Architecture yükleme engeli çözülmedi. Güvenlik
politikasını kapatmak, alternatif loader ile aşmak veya eski OpenAPI'den SDK üretmek kapanış kanıtı değildir.
Politikayı yöneten yetkili kişiyle güvenilir geliştirme/derleme yolu belirlenmesi gerekiyor.

## Uygulama sırası ve done when

12 Eylül karar eki: Satınalma faturası maliyet kaynağıdır; eksiye düşen çıkış son bilinen
maliyetini korur, sonraki alış yeni kendi maliyetiyle işlenir. DEC-011 ve Inventory sözleşmesi
güncellendi. Sonraki cevapla hiç maliyet geçmişi olmayan ürün için sıfır maliyet onaylandı;
bu başlangıç kararı artık açık değildir. Kalan değer farkının muhasebe hesabı hâlâ açıktır;
yeni alış için eski sevki otomatik yeniden fiyatlama uygulanmayacaktır.
Bu karar kaydında kod/DB değiştirilmedi; belge değişikliği nedeniyle test çalıştırılmadı.

1. DEC-011 kararını yazılı kaydet; aynı zamanda doğrulama ortamının politika engelini yetkili yoldan çöz.
2. Kalıcı sevk/allocation/consume ve miktar+değer+GL zincirini aynı transaction sözleşmesiyle tamamla.
3. Fatura ve iadeyi kaynak satır, cari/vergi/GL karşılıklarıyla uygula.
4. HTTP/OpenAPI/SDK ve web operasyon akışını tamamla; uyumluluk değişikliklerini değerlendir.
5. Kullanıcının MP-sonu test kadansına göre birleşik test paketini çalıştır, başarısızlıkları düzelt ve tekrar doğrula.
6. Master'daki beş çıkış kapısının her birine runtime/golden kanıtı bağla; ancak bundan sonra faz completed olur.

Bu değerlendirmede kod, DB, grant, güvenlik ayarı veya deployment değiştirilmedi. Yeni test/derleme
çalıştırılmadı; var olan sonuçlar yeni kapanış kanıtı sayılmadı. Commit/push yapılmadı.
