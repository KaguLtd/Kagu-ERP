# MP-04 sevk için güncel stok kartı doğrulaması

- Amaç: Kalıcı taslaktan sonra değişen ürün, şirket aktivasyonu ve depo durumunun sevk hazırlığını geçersiz kılabilmesi.
- Master fazı: MP-04; SALES-DSP-005 / INV-RES-015. Risk: R3 (yetki ve stok bütünlüğü).
- Durum: validating (runtime kabulü bekliyor). Sahip: atanmadı. Başlangıç: 11 Eylül 2026.
- Okunan belgeler: AGENTS, MASTER_PLAN MP-04 giriş/çıkış ve DoR; docs/README;
  veri mimarisi; Inventory miktar, tracking ve maliyet; önceki sevk planı ve kapanış değerlendirmesi.
- DoR: Mevcut Inventory-owned stock master loader aktif untracked stock, şirket ataması,
  depo ve miktar ölçeğini FOR SHARE ile doğrular. Yeni iş politikası gerekmiyor.

## Kapsam ve sınırlar

Rezervasyon önizlemesi tüm demand/position kilitlerini aldıktan sonra her satır için mevcut
stok master doğrulamasını çalıştırır. Tarihsel taslak okuma/replay değişmez. Güncel hazırlık
pasif veya desteklenmeyen karttan ilerlemez; lot/seri seçimi uygulanmadan bu ürünler açılmaz.
Eksi stok izni değişmez: on-hand yeterlilik şartı eklenmez.

Migration, yeni yetki, public API, GL, maliyet veya fiziksel stok hareketi yok. Mevcut typed
master-unavailable hatası kullanılır; başarısız hazırlıktaki audit aynı savepoint ile geri alınır.
Özel veri loglanmaz. Deployment iç kod güncellemesidir; veri telafisi gerekmez.

## Done when / doğrulama

- Güncel master guard önizlemeye bağlanmış olmalı.
- Aktif kart olumlu senaryosu korunmalı; pasif ürün/şirket/depo ve ölçek negatifleri hazırlanmalı.
- Hatalı önizleme audit veya lifecycle değişikliği bırakmamalı.
- Dar compile ve diff incelemesi ayrı kanıt; runtime/DB testleri MP-sonu paketine bırakılır.
- MP kapısı ancak gerçek test kanıtıyla değişir. Commit/push yok.

## İlerleme

- Mevcut Inventory-owned loader tüm demand/position kilitlerinden sonra, item/depo sırasıyla
  her satırda çağrılıyor. Transaction boyunca master FOR SHARE kilitleri korunur. Eksik kart,
  pasif item/item-company/warehouse ve geçersiz miktar ölçeği aynı typed hataya gider.
- PostgreSQL fixture'a üç pasif master negatifi, adet ürününde 0.5 reddi, tarihsel draft'ın
  okunabilir kalması ve failed preview audit sayacının değişmemesi eklendi. Değişiklikler
  savepoint içinde geri alınır; gerçek veriye veya kalıcı fixture durumuna yazılmaz.
- `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release
  --no-restore -v:q`: 11 Eylül 2026, başarılı; 0 uyarı, 0 hata; 30.94 sn.
  Bu derleme bağlı Inventory/Bootstrap projelerini de kapsar; runtime testi değildir.
- `git diff --check`: başarılı. Kod, master loader ve 0043 sürüm/artış trigger sözleşmesi
  incelendi; SQL fixture'ları exact version+1 kullanır.
- Runtime/DB/güvenlik negatifleri kullanıcı MP-sonu kadansı nedeniyle çalıştırılmadı.
  Pasiflik yarışı, RLS ve audit atomikliği gerçek DB kanıtı hâlâ bekliyor. Mevcut
  CodeIntegrity API/OpenAPI engeli bu oturumda yeniden denenmedi veya aşılmadı.
- Master kapısı ilerlemedi. Gerçek sevk/consume, maliyet ve GL kesinleştirmesi hâlâ açık.
