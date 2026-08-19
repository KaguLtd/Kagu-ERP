# IAM — Kimlik, Roller ve Yetkilendirme

## 1. Amaç ve sınır

IAM; insan ve servis kimliklerinin doğrulanması, uygulama profili, rol/permission, veri kapsamı, oturum, MFA, delegation, görevler ayrılığı ve acil erişimden sorumludur. Parolaları ERP veritabanında tutmaz; kimlik sağlayıcı Keycloak'tır.

## 2. Sorumluluk ayrımı

| Keycloak | ERP IAM |
|---|---|
| Parola, MFA credential, login, token, session, realm/client | Business user profile, role, permission, company/branch/depo/banka scope, onay limiti |
| Brute-force, password policy, recovery | Kullanıcının aktifliği, çalışan/cari bağlantısı, SoD, delegation, audit |

Keycloak admin olmak ERP finans verisine erişim vermez. ERP sistem yöneticisi de varsayılan olarak mali belge göremez.

## 3. Temel varlıklar

- `UserProfile`: `subject_id`, person reference, locale, timezone, active dates.
- `Role`: İş rolü; sistem ve custom.
- `Permission`: `resource.action`, ör. `invoice.post`, `cost.view`.
- `RolePermission`: Allow kayıtları; deny yalnız açık gerekçeyle ve politika tasarımıyla.
- `UserRole`: Tenant/company bağlamında rol ataması, başlangıç/bitiş.
- `UserScope`: Company, branch, warehouse, bank account, cash account, cost center kapsamı.
- `ApprovalLimit`: İşlem türü, para/fonksiyonel limit ve tarih etkisi.
- `Delegation`: Kimden, kime, hangi permission/scope, süre, gerekçe.
- `AccessReview`: Periyodik yetki doğrulama kampanyası.
- `EmergencyAccess`: Break-glass istek, onay, süre, kullanım kaydı.

## 4. Permission standardı

```text
<resource>.<action>
party.read
party.create
party.bank-account.change
inventory.cost.view
sales-order.submit
invoice.post
payment.approve
period.reopen
audit.export
security.role.assign
```

- `manage` gibi geniş permission mümkün olduğunca kullanılmaz.
- Read ve sensitive field view ayrılır.
- Export, print ve attachment download ayrıca yetkilendirilir.
- Permission kodu değişmez API sözleşmesidir; rename migration ister.

## 5. Başlangıç rol kataloğu

| Rol | Ana yetki | Varsayılan kısıt |
|---|---|---|
| Sistem yöneticisi | Teknik config, kullanıcı/OIDC | Mali veri ve belge posting yok |
| Şirket yöneticisi | Company ayar/rol atama | Kritik rol ikinci onay |
| Finans müdürü | Banka/kasa, ödeme, limit, dönem | Kendi hazırladığı ödemeyi onaylayamaz |
| Muhasebe sorumlusu | Posting, hesap planı, vergi, kapanış | Posted edit yok |
| Finans personeli | Tahsilat/ödeme/ekstre/çek | Atanmış hesap ve limit |
| Satış | Cari sorgu, teklif/sipariş | Maliyet/marj maskeli; iskonto onaylı |
| Satın alma | Talep/teklif/sipariş | Tedarikçi/IBAN ve limit kontrolü |
| Depo | Kabul/sevk/transfer/sayım | Atanmış depo; maliyet gizli olabilir |
| Yönetici/onaycı | Dashboard ve görev | Tutar ve kapsam limiti |
| Denetçi | Salt okunur rapor/audit | Değişiklik yok, süreli erişim |

Roller başlangıç şablonudur; doğrudan Keycloak realm role'a bağımlı business logic yazılmaz.

## 6. Yetki değerlendirme algoritması

1. Token/session issuer, audience, expiry ve subject doğrula.
2. `UserProfile` aktif ve tenant üyeliği geçerli mi?
3. Eylem için permission var mı?
4. Resource tenant/company scope içinde mi?
5. Branch/warehouse/bank/cost-center gibi alt scope uyuyor mu?
6. Durum ve koşul: tutar limiti, creator ≠ approver, dönem açık, field policy.
7. Delegation varsa süre ve kaynak permission sınırında mı?
8. Karar security audit/metric olarak güvenli biçimde kaydedilir.

Resource önce scope filtreli sorguyla bulunur; sonradan kontrol edip veriyi belleğe sızdırma.

## 7. Field-level policy

Hassas alan kümeleri:

- `financial-cost`: maliyet, marj, alış fiyatı.
- `banking-sensitive`: IBAN, hesap numarası, banka dosyası.
- `personal-sensitive`: kimlik/vergi no, adres, telefon/e-posta.
- `security-sensitive`: role, session, audit export.

API unauthorized alanı `null` ile belirsiz bırakmak yerine contract'a göre omit/masked döndürür. Filtre/sort/export üzerinden yan kanal da engellenir.

## 8. Login ve MFA

- Tüm yönetici, muhasebe, finans ve onaycı rollerinde MFA zorunlu.
- Keycloak production mode, sabit public hostname ve ayrı admin hostname.
- Recovery code ve credential reset helpdesk doğrulama prosedürü ister.
- Finansal kritik eylem için step-up: yeni IBAN onayı, yüksek ödeme, dönem reopen, role escalation, legal export.
- Session idle ve absolute timeout rol riskine göre.
- İşten ayrılan kullanıcı disable + session revoke + delegation iptali aynı süreçte.

## 9. Web ve mobil oturum

- Web: HttpOnly/Secure cookie; CSRF; token JS'ye verilmez.
- Android: system browser + PKCE; access token kısa ömür; Keystore-backed refresh; logout/revoke yerel cache temizler.
- Aynı kullanıcı cihaz listesi ve son kullanımını görebilir; yönetici token içeriğini göremez.
- Riskli cihaz/root/debug sinyali tek başına hukuki karar değil; policy ile blok veya read-only.

## 10. Görevler ayrılığı

Zorunlu çatışma örnekleri:

- Tedarikçi/IBAN değiştiren aynı ödemeyi onaylayamaz.
- Ödeme hazırlayan aynı ödemeyi final onaylayamaz.
- Stok sayımı giren farkı tek başına post edemez.
- Journal hazırlayan kapanış/reopen onayını tek başına veremez.
- Role atayan kendi yetkisini yükseltemez.
- Çeki kaydeden kritik ciro/karşılıksız durumunu tek başına kesinleştiremez.

SoD kontrolü yalnız UI'da değil application policy ve testte.

## 11. Break-glass

- Normal rol çözmüyorsa gerekçeli istek, MFA ve ayrı onay.
- En fazla belirli süre; otomatik expire.
- Session banner ve her eylemde `emergency_access_id`.
- Anlık güvenlik bildirimi ve sonraki iş günü zorunlu inceleme.
- Break-glass posted kayıt değiştiremez ve audit kapatamaz.

## 12. API yüzeyi

```text
GET  /api/v1/me
GET  /api/v1/me/scopes
GET  /api/v1/users
POST /api/v1/users/{id}/roles
POST /api/v1/users/{id}/scopes
POST /api/v1/delegations
POST /api/v1/access-reviews
POST /api/v1/emergency-access/requests
POST /api/v1/emergency-access/{id}/approve
POST /api/v1/sessions/{id}/revoke
```

Role/scope mutation `If-Match`, idempotency, ikinci onay ve audit gerektirir.

## 13. Audit olayları

- Login success/failure, MFA/reset, logout/revoke.
- User enable/disable.
- Role/permission/scope/limit/delegation değişimi önce/sonra.
- Sensitive view/export ve impersonation.
- Break-glass lifecycle.
- Authorization deny için PII'siz güvenlik olayı ve yüksek hacimde sampling/aggregation.

## 14. Test ve kabul

- [ ] Tenant/company/branch/depo/banka IDOR negatif matrisi.
- [ ] Maliyet alanı response, filter, sort ve exportta maskeli.
- [ ] Hazırlayan kendi ödeme/onayını yapamıyor.
- [ ] Delegation, verenin sahip olmadığı yetkiyi veremiyor ve sürede bitiyor.
- [ ] Disable sonrası mevcut web/mobil session revoke oluyor.
- [ ] Web tokenı browser storage'da yok; CSRF testi geçiyor.
- [ ] Android yanlış issuer/audience ve stale tokenı reddediyor.
- [ ] Break-glass süre sonunda kapanıyor ve tüm eylemler raporlanıyor.
- [ ] RLS pooled-connection tenant sızıntı testi geçiyor.

## 15. İç kontrol ve çok katmanlı yetki ekleri

Yetkilendirme yalnız menü/rol eşlemesi değildir. Her command şu katmanları birlikte değerlendirir:

1. model/resource permission;
2. tenant/company/branch/warehouse/bank record scope;
3. field policy ve hassas değer maskeleme;
4. mevcut state’e göre action/button permission;
5. tutar, iskonto, risk ve dönem gibi koşullar;
6. SoD, quorum ve delegation çatışması.

Onay quorum’u iki ise iki farklı insan gerekir; aynı kimlik, grup üyeliği veya vekâlet zinciri iki oyu karşılayamaz. Approver, isteği hazırlayanın doğrudan hesabı dışında aynı service identity veya paylaşılan kullanıcı üzerinden de olamaz.

Erişim gözden geçirmesi en az üç ayda bir yüksek riskli permission, dormant kullanıcı, geçici rol, break-glass ve scope genişlemelerini raporlar. Kontrol sahibi her bulguyu retain/revoke/exception kararıyla imzalar. Ayrıcalıklı role atama, banka hesabı değişikliği, posting/reopen/repost ve kullanıcı–rol değişikliği kendiliğinden denetim örneklemine girer.

Public API method parametreleri güvenilir sayılmaz; permission kontrolünden sonra kaynak nesne tekrar yüklenir ve state/scope server tarafında doğrulanır. Read model veya export, kaynak endpoint’ten daha geniş yetki veremez.
