# ADR-0003 — Web ve Mobil Yalnız API Üzerinden Erişir

- Durum: Accepted — güvenlik değişmezi
- Tarih: 2026-08-18
- Son doğrulama: 2026-08-19 — business-command API ilkesiyle

## Bağlam

Web ve Android'in aynı veriye erişmesi gerekir. Doğrudan DB bağlantısı credential dağıtır, iş kurallarını atlar, schema'yı istemciye bağlar, audit/yetki ve ağ güvenliğini bozar.

## Karar

Tüm insan ve dış istemciler sürümlü HTTPS API kullanır. PostgreSQL yalnız özel ağda API/worker/migration/backup rollerine açıktır. İş kuralları ve authorization sunucudadır. Web/Android OpenAPI contract'ından tipli istemci üretir.

## Sonuçlar

- Tek yetki/audit/idempotency noktası ve kontrollü API evrimi.
- Mobil offline cache/sync ayrıca tasarlanır.
- API kullanılabilirliği tüm istemciler için kritik; SLO ve backward compatibility gerekir.

## Reddedilenler

- Mobil/web için DB user veya exposed 5432.
- Supabase-benzeri otomatik tablo API'sinin domain komutlarının yerine geçmesi.
- UI'ın doğrudan bir modül şemasını sorgulaması.

## Uygulama kanıtı

Firewall/Compose dış DB portu yok; repository'de connection string yalnız server; istemci dependency scan; authorization ve şirket izolasyon E2E testleri.

## v1.1 açıklaması

API-only, tablo CRUD API’si anlamına gelmez. Post, reverse, allocate, unallocate, reconcile, close/reopen ve repost açık domain command’larıdır. Sunucu resource’u yeniden yükleyip permission, scope, field/action policy, current state, period lock, SoD, expected version ve idempotency’yi doğrular.

Web/Android business, accounting, allocation, bank ve integration durumlarını ayrı API alanlarından okur; “paid” veya tek status türetmez. Report response as-of, watermark ve projection generation taşır. Mobil offline command sunucu kabulü olmadan posted sayılmaz.
