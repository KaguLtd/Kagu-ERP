# AGENTS.md — Android istemcisi ek kuralları

Bu klasörde kök `AGENTS.md` kurallarına ek olarak aşağıdakiler geçerlidir.

- UI doğrudan ağ, Room veya DAO çağrısı yapmaz; ViewModel ve repository sınırları korunur.
- OIDC Authorization Code + PKCE sistem tarayıcısında çalışır; WebView login ve gömülü client secret yasaktır.
- Token düz SharedPreferences, log, backup veya ekran görüntüsüne yazılmaz.
- Offline finansal kesinleştirme yapılmaz; queued, accepted ve posted durumları ayrı gösterilir.
- Şirket değişiminde cache kapsamı değişir veya temizlenir; cihaz saati iş kuralı kaynağı değildir.
- Compose semantics, dinamik yazı tipi ve en az minSdk/targetSdk senaryoları test edilir.
- UI katmanı para hesabı yapmaz; decimal API değerleri kayıpsız metin/değer nesnesi olarak taşınır.

