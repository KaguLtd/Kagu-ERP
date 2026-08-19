# Güvenlik Mimarisi ve Tehdit Modeli

## 1. Hedef

Finansal kayıtların bütünlüğünü, şirket verisinin gizliliğini, sistemin kullanılabilirliğini ve denetim izini korumak. Hedef kontrol seviyesi OWASP ASVS 5.0 Level 2'dir; ödeme/e-fatura/yönetici işlemlerinde risk bazlı ek kontroller uygulanır.

## 2. Korunan varlıklar

- kullanıcı kimliği, MFA ve oturumlar,
- cari kişisel verileri, VKN, iletişim ve banka hesapları,
- fatura, fiyat, stok, mali rapor ve ticari sırlar,
- muhasebe fişleri ve e-fatura resmi arşivi,
- ödeme talimatı, çek/senet ve banka ekstresi,
- secret, sertifika, yedek ve şifreleme anahtarları,
- audit, log, metric ve incident kanıtları,
- kaynak kod, build pipeline ve release artefaktları.

## 3. Aktörler ve güven sınırları

Aktörler: normal çalışan, finans/muhasebe, yönetici, sistem yöneticisi, denetçi, entegrasyon sağlayıcı, destek personeli, ele geçirilmiş hesap/cihaz ve dış saldırgan.

Sınırlar:

1. internet ↔ Caddy edge,
2. tarayıcı/Android ↔ API,
3. API ↔ Keycloak,
4. API/worker ↔ PostgreSQL/blob store,
5. worker ↔ banka/e-fatura/e-posta,
6. üretim ↔ yedek hedefi,
7. CI/CD ↔ üretim dağıtımı,
8. destek/operasyon ↔ hassas yönetim yüzeyi.

Her sınır kimlik, yetki, şifreleme, doğrulama, log ve zaman aşımı kontrolüne sahiptir.

## 4. Başlıca tehditler ve kontroller

| Tehdit | Birincil kontroller |
|---|---|
| Hesap ele geçirme | MFA, kısa oturum, rate limit, riskli işlemde re-auth, oturum iptali |
| Şirketler arası veri sızıntısı | `company_id`, merkezi policy, RLS, negatif entegrasyon testi |
| Finansal kayıt oynama | append-only sonuç, ters kayıt, görev ayrılığı, audit/hash |
| Çift ödeme/fatura | unique constraint, idempotency, durum sorgusu, maker-checker |
| SQL/komut enjeksiyonu | parametreli sorgu, allowlist, process izolasyonu, güvenli XML/CSV |
| XSS/CSRF | framework escaping, CSP, sanitization, HttpOnly cookie, CSRF token |
| SSRF | egress allowlist/proxy, URL allowlist, metadata ağı engeli |
| Zararlı dosya | karantina, MIME/imza, AV, boyut/açma limiti, ayrı origin indirme |
| Secret sızıntısı | secret store, rotasyon, log redaction, CI secret scan |
| Supply-chain | lockfile, SCA, SBOM, imzalı/özetli image, kontrollü registry |
| Ransomware/veri kaybı | ağdan ayrık/immutable yedek, ayrı kimlik, restore tatbikatı |
| Yönetici kötüye kullanımı | least privilege, JIT/break-glass, çift kontrol, ayrı audit |
| Mobil cihaz kaybı | Keystore, minimal cache, kısa oturum, uzaktan iptal/MDM |

## 5. Kimlik ve erişim

- Keycloak OIDC; kişiye özel hesap, ortak kullanıcı yok.
- MFA finans, yönetici, destek ve uzaktan erişimde zorunlu; diğer rollerde risk/politikaya göre.
- Service account insan UI'sına giremez; kapsamlı kısa ömürlü credential tercih edilir.
- RBAC temel rolü, şirket/şube/depo kapsamı ve işlem politikasıyla birleşir.
- “Deny by default”; yeni endpoint açık yetki politikası olmadan build/review geçmez.
- Yetki değişikliği oturum/cache'e kontrollü sürede yansır; kritik kaldırma hemen iptal eder.
- Break-glass hesap kapalı/korumalı, donanım MFA, kullanınca anlık alarm ve sonradan inceleme.
- Periyodik erişim gözden geçirme ve işten ayrılma otomasyonu yapılır.

## 6. Oturum ve istemci

Web tokenı browser storage'a yazmaz; cookie/BFF modeli, CSRF ve sıkı origin kontrolü. Android Authorization Code + PKCE ve sistem tarayıcısı kullanır. Redirect URI tam allowlist; wildcard yok. Logout yalnız UI state temizliği değildir, sunucu oturumu/refresh iptali yapar.

Riskli eylemler: ödeme onayı, banka hesabı değişikliği, rol verme, dönem açma, yedek indirme, e-fatura entegrasyon ayarı. Bunlarda yakın zamanda MFA/re-auth ve işlem özeti gerekir.

## 7. Uygulama ve API kontrolleri

- Pozitif model doğrulama; uzunluk, tip, enum, para ve dosya sınırları.
- ORM parametreli sorgu; dinamik sort/filter allowlist.
- `ProblemDetails` hassas iç ayrıntı döndürmez.
- Rate limit giriş, arama, export, dosya, webhook ve ağır rapora ayrı.
- Idempotency ve ETag finansal komutlarda.
- CORS yalnız gerekli origin; credentials + wildcard yasak.
- CSP, `frame-ancestors`, `nosniff`, Referrer/Permissions Policy ve HSTS edge'de.
- XML dış entity kapalı; zip bombası ve CSV formül enjeksiyonu engeli.
- Kullanıcı içeriği log/HTML/template'e güvenli kodlanır.

## 8. Veri tabanı ve depolama

- Uygulama migration, runtime read/write, worker, audit ve backup için ayrı DB rolleri.
- Runtime rolü owner/superuser değildir; public schema create kapalı.
- RLS defense-in-depth; app katmanı da kapsam uygular.
- Connection pool kiracı/şirket context'ini iade öncesi sıfırlar.
- TLS iç ağda risk/model kararına göre; host dışına açık DB portu yok.
- Disk/volume ve yedekler şifreli; anahtar yedekten ayrı.
- Hassas alanlar gerekirse uygulama/kolon seviyesinde şifrelenir; arama/rapor etkisi ADR ile.
- Üretim DBA erişimi kişisel, MFA/VPN, zaman sınırlı ve auditli.

## 9. Sunucu ve ağ

- Yalnız 22 (sınırlı yönetim), 80/443 dışarı açık; 80 HTTPS'e yönlenir.
- SSH anahtar/MFA/VPN; parola ve root login kapalı; allowlist/fail2ban risk bazlı.
- Containerlar rootless veya minimum capability, read-only filesystem mümkün olanlarda.
- DB/Keycloak admin/metrics dış internete açılmaz.
- Docker socket uygulama container'ına bağlanmaz.
- Güvenlik güncellemeleri, zaman senkronu, firewall, disk ve sertifika alarmı.
- Egress bank/e-fatura gibi izinli hedeflerle sınırlandırılır; DNS ve metadata koruması.

## 10. Secret ve kriptografi

- Secret Git, image, compose dosyası veya log içinde yoktur.
- Üretim secret'ı CI kullanıcılarından ayrılır; en az yetkiyle runtime'a enjekte edilir.
- Rotasyon sahibi/süresi; çift anahtar geçişi ve acil iptal planı.
- Standart, güncel platform kriptografisi; özel algoritma tasarlanmaz.
- Parola doğrulama Keycloak'a bırakılır; uygulama parola tutmaz.
- Yedek ve e-fatura arşiv anahtarlarının kaybı da felaket senaryosudur; escrow/çift kontrol planı.

## 11. Yazılım tedarik zinciri

- Korunan ana dal, zorunlu review ve imzalı/izlenebilir release.
- Bağımlılıklar lockfile ile sabit; otomatik PR + test + lisans/SCA.
- Her release için SBOM ve image digest.
- Base image küçük ve destekli; `latest` tag kullanılmaz.
- CI runner secret izolasyonu; fork/untrusted kod prod secret'a ulaşmaz.
- Artefakt registry erişimi ve retention kontrollü; mümkünse provenance/imza doğrulama.

## 12. Gizlilik ve KKTC kişisel veri

- Amaç/veri envanteri, hukuki dayanak, erişim, saklama ve silme/anonimleştirme politikası.
- Log, test, analytics ve support ekranında veri minimizasyonu.
- Veri sahibi talebi iş akışı hukuki onay ve finansal saklama çakışmasını yönetir.
- Yurt dışı servis, bulut yedeği, crash analytics, push/e-posta sağlayıcıları veri transferi değerlendirmesinden geçer.
- Gerekli veri transfer ruhsatı/kurul işlemi tamamlanmadan kişisel veri dış hedefe aktarılmaz.

## 13. Güvenlik gözlemlenebilirliği ve olay

Alarm örnekleri: ardışık login/MFA hatası, yeni admin/rol, break-glass, banka hesabı değişimi, toplu export, kapalı dönem açma, audit hatası, backup başarısızlığı, AV bulgusu, e-fatura veri bozulması.

Loglar merkezi, zaman senkronlu, erişim kontrollü ve silinmeye karşı korumalıdır. Olay akışı [incident runbook'unda](../operations/03-observability-and-incident-response.md) ve e-fatura yasal saatleri [restore planında](../operations/02-backup-restore-disaster-recovery.md) yer alır.

## 14. Doğrulama ve canlı kapısı

- ASVS L2 kontrol–gereksinim–test matrisi tamamlanmış.
- Tehdit modeli her önemli mimari/entegrasyon değişiminde güncel.
- Kritik/yüksek SAST/SCA/image/pen-test bulgusu kapalı veya süreli risk kabulü.
- Yetki/RLS negatif testleri tam.
- Secret rotasyon ve break-glass tatbikatı yapılmış.
- Restore/ransomware ve incident tabletop başarılı.
- Veri transferi ve hukuki onay soruları kapatılmış.

## 15. Muhasebe hilesi ve yetki kötüye kullanımı tehditleri

| Tehdit | Önleyici kontrol | Tespit edici kontrol |
|---|---|---|
| Sahte/duplicate tedarikçi faturası | natural/external key, 2/3/4-way match, approval | duplicate similarity ve aynı tutar/reference analizi |
| IBAN değiştirip ödeme | ayrı permission, bağımsız doğrulama, payment hold | değişiklik→ödeme yakınlık alarmı |
| Kendi işlemini onaylama | maker-checker, distinct-person quorum | SoD conflict raporu |
| Kapalı döneme geri yazma | scope lock, backdate permission/impact | post-filing/close change raporu |
| Posted hareketi silme/değiştirme | DB privilege/trigger, append-only | integrity/checksum ve reconciliation |
| Allocation ile bakiyeyi gizleme | sınır constraint, ayrı unallocation event | stale/unusual reallocation raporu |
| Sahte banka mutabakatı | immutable statement, maker-checker | statement balance/control totals |
| Repost ile geçmişi değiştirme | dry-run, approval, source checksum/generation | old/new diff ve audit |
| Yetkisiz export/maliyet görüntüleme | field/scope authz | export volume ve access anomaly |

COSO yaklaşımıyla her tehdit yalnız teknik kontrol değil; control owner, sıklık, kanıt, exception SLA ve bağımsız review taşır.

## 16. Komut ve kayıt yetkilendirmesi

Public application method parametreleri güvenilir değildir. Endpoint:

1. kimlik/token ve permission’ı;
2. DB’den yeniden yüklenen resource scope’unu;
3. field/action policy’yi;
4. current state, lock ve SoD’yi;
5. expected version/idempotency’yi

server tarafında doğrular. UI allowedActions yalnız kolaylıktır. Background job aynı tenant/company security context’i ve least-privilege service identity taşır; system user global bypass değildir.

Rapor, materialized view, attachment, search index ve export kaynak endpoint’ten geniş veri açamaz. Count/total, timing ve error mesajları da şirket/field gizliliğini sızdırmamalıdır.

## 17. Finansal bütünlük olayları

Subledger–GL farkı, statement control-total farkı, source-less journal, duplicate projection, broken reversal chain, sequence anomaly veya archive hash mismatch security/financial-integrity incident sınıfıdır. Kritik seviyede posting veya period close fail-closed olabilir; otomatik repair önce kanıt snapshot’ı alır ve onaylı runbook izler.

Audit/log immutability tek başına doğruluk garantisi değildir; source→ledger→report reconciliation ile birlikte değerlendirilir.
