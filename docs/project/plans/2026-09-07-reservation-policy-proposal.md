# MP-04 Rezervasyon davranışı karar önerisi

Durum: approved — uygulama sorusuna verilen Devam yanıtıyla DEC-MP01-025 olarak kaydedildi.
Bağlantı: DEC-MP01-011, Inventory reservation lifecycle ve Sales dispatch preparation planları.

## Karar verilmesi gereken davranışlar

1. Depo: Sipariş onaylanırken kullanıcı yetkili olduğu depoyu seçsin; rezervasyon seçilen depoya bağlı olsun. İlk sürümde sipariş satırı tek depodan ayrılsın. Depolar arası otomatik dağıtım bu dilimde yapılmasın.
2. Yetersiz stok: Sipariş kaydedilebilsin; elde olmayan miktar rezerve veya sevk edilemesin. Kullanıcı mevcut miktar kadar kısmi rezervasyon yapabilsin, kalan sipariş açık beklesin. Bu öneri confirm'da tüm miktarı ayırma zorunluluğu yerine sipariş durumu ile rezervasyonu ayrı tutar; kabul edilirse Sales confirm sözleşmesi buna göre güncellenmelidir.
3. Rezervasyon süresi: İlk sürümde otomatik süre sonu olmasın. Sevkle tüketilsin, sipariş iptalinde kalan rezervasyon serbest bırakılsın. Manuel serbest bırakma açık yetki ve gerekçe istesin; izin kodu uygulama aşamasında sözleşmeye kaydedilsin.

Kullanıcı yanıtı DEC-MP01-025 kaydındadır; miktar rezervasyonu için ürün kararı beklenmiyor. Teknik persistence ve concurrency kanıtları tamamlanmalıdır.

## Teknik uygulama sorumluluğu

Stok pozisyonu için ortak transaction kilidi, kilitlerin deterministik sırası, exact decimal aritmetik, idempotency, RLS ve audit uygulama konusudur; kullanıcıdan kilit algoritması seçmesi istenmez. Concurrency testleri MP-04 toplu kapısında yapılır. Lock protokolü yalnız bütün stok çıkışı/transfer/rezervasyon writer'ları katıldığında koruma sağlar.

## Sonraki kapı

Bu üç karar miktar rezervasyonu persistence dilimini açar. Maliyet/GL posting için DEC-MP01-011 değerleme ve backdate kararları ayrıca gereklidir; bu öneri onları çözülmüş saymaz. Mevcut ilk-sevk sorgusu salt hazırlık olarak kalır ve posting açılmadan persisted allocation kaynağına bağlanır.
