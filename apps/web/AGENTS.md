# AGENTS.md — Web istemcisi ek kuralları

Bu klasörde kök `AGENTS.md` kurallarına ek olarak aşağıdakiler geçerlidir.

- TypeScript `strict` kapatılamaz; yeni `any` kullanımı gerekçesiz eklenemez.
- Sunucu durumu TanStack Query'de tutulur. İş verisi istemci global store'una kopyalanmaz.
- Web oturumu same-origin cookie/BFF sınırında kalır; token tarayıcı storage alanlarına yazılmaz.
- UI görünürlüğü sunucu yetkilendirmesinin yerine geçmez.
- Para API sözleşmesinde decimal string olarak taşınır; `number` ile parasal hesap yapılmaz.
- Feature klasörleri başka feature'ın iç dosyalarını import etmez; yalnız yayımlanmış giriş noktalarını kullanır.
- Kullanıcı davranışı değişikliğinde component testi; kritik akışta Playwright testi eklenir.
- Etkileşimli öğeler klavye, görünür odak ve semantik ad gerektirir; durum yalnız renkle anlatılmaz.

