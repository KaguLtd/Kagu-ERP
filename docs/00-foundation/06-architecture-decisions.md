# 06 — Mimari Kararlar ve Yeniden Değerlendirme Kapıları

Bu belge karar özetidir. Önemli yeni kararlar `docs/decisions/ADR-XXXX-*.md` ile kaydedilir.

## Kabul edilmiş kararlar

| ADR | Karar | Durum | Yeniden değerlendirme tetikleyicisi |
|---|---|---|---|
| [0001](../decisions/ADR-0001-modular-monolith.md) | Modüler monolit | Accepted | Ekip/modül bağımsız release veya farklı ölçek ihtiyacı ölçülürse |
| [0002](../decisions/ADR-0002-technology-stack.md) | .NET/PostgreSQL/React/Kotlin | Accepted | LTS/EOL, kritik uyumsuzluk veya doğrulanmış maliyet sorunu |
| [0003](../decisions/ADR-0003-api-only-clients.md) | İstemciler API-only | Accepted, değişmez | Yeniden değerlendirilmez; güvenlik sınırı |
| [0004](../decisions/ADR-0004-identity-and-session.md) | Keycloak + web BFF/cookie + mobil PKCE | Accepted | Sağlayıcı desteği/lisans/HA ihtiyacı değişirse |
| [0005](../decisions/ADR-0005-single-server-compose.md) | İlk prod tek Linux host + Compose | Accepted | SLO, kapasite veya felaket alanı ihtiyacı yetersiz kalırsa |
| [0006](../decisions/ADR-0006-append-only-financial-records.md) | Finansal hareketler append-only | Accepted, değişmez | Yalnız yeni mevzuat; bütünlük ilkesi korunur |

ADR-0006’nın v1.1 yorumu bağlayıcıdır: kaynak ekonomik olay append-only düzeltme zinciri taşır; alt defter/GL/read model türetilmiş olsa da önceki generation auditten kaybolmaz. Payment, allocation ve bank reconciliation ayrı yaşam döngüleridir. Bu açıklama yeni dosya/ADR üretmeden mevcut kararın kapsamını netleştirir.

## Karar verme ölçütleri

Yeni teknik seçim şu sırayla değerlendirilir:

1. Finansal bütünlük ve veri kaybı riski.
2. Güvenlik, tenant izolasyonu ve kişisel veri konumu.
3. Operasyonel sadelik ve restore edilebilirlik.
4. Resmi destek/LTS ve bakım topluluğu.
5. Test edilebilirlik ve gözlemlenebilirlik.
6. Ekip yetkinliği ve işe alım.
7. Performans ve maliyet.
8. Geliştirici deneyimi.

## Build vs buy sınırları

### Kendimiz geliştiririz

- ERP domain, posting, stok/cari, KKTC vergi ve belge kuralları.
- Yetki scope entegrasyonu ve iş akışları.
- Web/Android iş deneyimi.
- Entegrasyon adapter contract'ları ve mutabakat.

### Olgun bileşen kullanırız

- OIDC/MFA: Keycloak.
- DB: PostgreSQL.
- TLS/edge: Caddy.
- Telemetry standardı: OpenTelemetry.
- UI primitive: shadcn/ui/Radix tabanı.
- Backup: pgBackRest/restic.

### Satın alma/sağlayıcıya bırakılır

- Onaylı mali yazar kasa donanımı.
- Banka ve resmi e-Fatura erişim yetkisi/sertifikası.
- SMS/e-posta teslim altyapısı.
- Uzak immutable storage, hukuki konum onayı sonrası.

## Teknoloji ekleme kapısı

Bir dependency veya platform eklemek için ADR şunları kanıtlar:

- Çözülmekte olan ölçülmüş problem.
- Mevcut yığınla neden çözülemediği.
- Lisans, güvenlik, veri konumu ve bakım durumu.
- Failure mode, backup/restore ve gözlem.
- Exit/migration planı.
- Test ve operasyon sahipliği.

## Özellikle ertelenen kararlar

- Bağımsız müşteriler için database-per-tenant mı shared/RLS mi: ilk dış müşteri öncesi.
- Yerel blob'dan S3-compatible store'a geçiş: veri transfer/onay ve hacim sonrası.
- Read replica/warehouse: rapor yükü transaction SLO'yu etkilediğinde.
- Message broker: outbox backlog ve entegrasyon hacmi DB worker sınırını aştığında.
- Kubernetes: en az üç node/çok servis/HA iş gerekçesi olmadan değil.
- Cross-platform mobil: iOS onaylı kapsam olduğunda Flutter/KMP yeniden değerlendirmesi.

## Araştırmayla doğrulanan karar kontrolü

Yeni bir ERP davranışı kabul edilirken ADR veya görev planı şu dört soruyu yanıtlar:

1. Kaynak taahhüt/ekonomik olay nedir; hangi alt defter ve GL kontrol hesabını etkiler?
2. Düzeltme business reversal mı, allocation değişikliği mi, yoksa yalnız projection repost mu?
3. Etkin tarih, kayıt/posting zamanı, dönem/vergi kilidi ve geçmiş tarihli değerleme etkisi nedir?
4. Hangi rapor ve mutabakat, kararın doğru çalıştığını bağımsız olarak kanıtlar?

Başka ERP’de bulunması tek başına karar gerekçesi değildir; clean-room, lisans, KKTC uyumu, bakım ve kullanıcı kabulü değerlendirilir.
