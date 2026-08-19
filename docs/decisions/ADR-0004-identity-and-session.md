# ADR-0004 — Keycloak ve İstemci Oturumları

- Durum: Accepted
- Tarih: 2026-08-18
- Son doğrulama: 2026-08-19 — kayıt/alan/eylem yetkisi ve SoD ile

## Bağlam

Roller, MFA, oturum iptali, web ve public Android client gerekir. Uygulamanın parola/MFA sistemi yazması gereksiz güvenlik riskidir.

## Karar

Keycloak OIDC identity provider. Web aynı-origin BFF/sunucu oturumu veya HttpOnly Secure cookie modeli; token browser storage'da değil. Android Authorization Code + PKCE, sistem tarayıcısı ve Keystore. API token/session doğrular, fakat permission + company/branch scope kararını uygulama politikası verir.

## Sonuçlar

- Olgun MFA/session/admin yetenekleri.
- Keycloak ayrı kritik DB/backup/upgrade sorumluluğu.
- Web CSRF; Android redirect/token güvenliği açıkça test edilir.
- Kullanıcı rolünün Keycloak grup claim'ine tamamen gömülmesi yerine ERP permission/scope modeli korunur.

## Reddedilenler

- Uygulama içi parola/MFA.
- SPA access/refresh tokenını localStorage'a koymak.
- Android client secret gömmek veya WebView login.

## Yeniden değerlendirme

Keycloak destek/operasyonunun karşılanamaması, kurumsal IdP federasyonu veya HA ihtiyacı. Güvenli OIDC/BFF/PKCE ilkeleri korunur.

## v1.1 açıklaması

Identity doğrulaması yetkilendirme değildir. ERP uygulaması model/resource permission, record scope, field visibility, current-state action ve distinct-person quorum kararlarını verir. Keycloak role/group claim’i yüksek riskli command için tek otorite olamaz.

Delegation ve break-glass ayrı auditli, süreli uygulama kayıtlarıdır; token claim’i değiştirerek SoD baypas etmez. Background worker least-privilege service identity ve explicit tenant/company context’i olmadan finansal kayıt işleyemez.
