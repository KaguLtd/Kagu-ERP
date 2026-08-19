# Linux Sunucu Kurulum ve Dağıtım Runbook'u

## 1. Hedef topology

İlk üretim: tek, iyi yedeklenen Linux host üzerinde Docker Compose. Sunucu yalnız web/API edge'ini internete açar; PostgreSQL, Keycloak yönetimi, metrics ve container portları özel ağdadır.

Önerilen işletim sistemi: **Ubuntu Server 26.04 LTS**; mevcut operasyon standardı 24.04 LTS ise desteklenen fallback olarak kullanılabilir. Kurulum anında OS, Docker Engine/Compose, PostgreSQL, Keycloak, Caddy ve .NET container sürümleri güncel resmi destek tablolarıyla doğrulanıp digest/paket sürümü sabitlenir.

```text
Internet
   │ 443
 Caddy
   ├── /        → Web static
   ├── /api     → ASP.NET Core API
   └── /auth    → Keycloak public endpoints

internal Docker networks
   ├── API ── PostgreSQL
   ├── Worker ── PostgreSQL/blob/external allowlist
   ├── Keycloak ── PostgreSQL (ayrı DB/rol)
   └── OTel Collector ── monitoring stack
```

Tek host yüksek erişilebilir değildir. İş hedefi %99,9+ veya host arızasında çok kısa RTO istiyorsa ikinci host/managed DB/failover ayrı ADR ister.

## 2. Ön koşullar

- Alan adları: ör. `erp.firma.com`, gerekirse ayrı `auth.firma.com`.
- DNS A/AAAA, ters DNS gerekleri ve statik public IP.
- 80/443 erişimi; yönetim için VPN/allowlist SSH.
- Host kapasite testi ve RAID/SSD/UPS kararı.
- Ayrı uzak ve mümkünse immutable yedek deposu.
- SMTP/push/e-fatura/banka endpoint ve resmi onayları.
- Üretim secrets/sertifikalar ve sahipleri.
- KKTC dışına veri gidecek tüm servisler için veri transferi kararı.
- İzleme/alarm alıcıları ve on-call listesi.

## 3. Host hardening

- Minimal OS; gereksiz paket/servis kapalı.
- Ayrı kişisel yönetici hesapları; root SSH ve parola ile SSH kapalı.
- SSH anahtarı + VPN/allowlist; mümkünse MFA/bastion.
- `ufw`/nftables: dışarı yalnız 80/443, yönetim kaynağından 22.
- Otomatik güvenlik güncellemesi veya kontrollü haftalık patch penceresi.
- NTP/chrony; zaman sapması alarmı.
- Audit/journal retention ve merkezi log gönderimi.
- Docker ve yedek komutları yalnız sınırlı `erpops` grubu; Docker grubunun root eşdeğeri olduğu kabul edilir.
- Disk şifreleme tehdit modeline göre; reboot anahtar erişimi ve uzaktan kurtarma planı.
- Swap/OOM, file descriptor, kernel/network ayarları ölçümle; internetten rastgele sysctl kopyalanmaz.

## 4. Sunucu dosya düzeni

```text
/opt/kktc-erp/
├── current/                 # sürümlü compose/config checkout
├── releases/<version>/
├── shared/
│   ├── config/              # secret olmayan config
│   ├── secrets/             # root/erpops, 0700; tercih edilen secret store mount
│   ├── caddy/
│   └── scripts/
└── logs/                    # yalnız host-level gerekiyorsa

/srv/kktc-erp/
├── postgres/
├── keycloak-postgres/
├── blobs/
├── quarantine/
├── exports/
└── observability/

/var/lib/pgbackrest/         # veya ayrı mount/repository
```

Kod/image host dizinine rastgele kopyalanmaz. `releases` değişmez manifest/digest taşır; `current` atomik bağlantı olabilir. DB/blob volume silme bakım komutlarına dahil değildir.

## 5. Compose hizmetleri ve ağlar

Hizmetler:

- `caddy`
- `web`
- `api`
- `worker`
- `postgres`
- `keycloak`
- `keycloak-postgres` (veya açıkça ayrılmış aynı cluster; ayrı rol/DB)
- `otel-collector`
- opsiyonel `prometheus`, `grafana`, `loki`, `tempo`
- `backup-runner` yalnız kontrollü job/container olarak.

Ağlar:

- `edge`: Caddy↔web/API/Keycloak public.
- `app`: API/worker iç iletişim.
- `data`: API/worker/identity↔DB; dış yayın yok.
- `observability`: collector/monitoring; admin arayüzü VPN/SSH tunnel.

`ports:` sadece Caddy için 80/443 ve gerekiyorsa loopback yönetim bind'i. DB için public `5432:5432` yoktur.

## 6. Container standardı

- Image digest ile dağıtım; `latest` yok.
- Non-root kullanıcı, minimum Linux capability, `no-new-privileges`.
- Mümkünse read-only root filesystem ve ayrı tmpfs.
- Healthcheck ile liveness; API readiness DB/critical dependency politikasını doğru yansıtır.
- CPU/RAM limit ve reservation ölçüme dayalı.
- Log stdout/stderr JSON; container içine sınırsız dosya logu yok.
- Secret image/env dump'a girmeyecek mount/secret yöntemi; `docker inspect` riski değerlendirilir.
- Docker socket hiçbir uygulama container'ına verilmez.
- Compose config üretim öncesi doğrulanır; render edilmiş config secret içeriyorsa güvenli tutulur.

## 7. Caddy ve TLS

- Caddy yalnız public edge; otomatik HTTPS için DNS ve 80/443 hazır.
- `erp` ve varsa `auth` hostname'i açıkça tanımlı; catch-all host proxy yok.
- Upstream container DNS adıyla, internal network'te.
- Güvenli headerlar: HSTS (hazırlık sonrası), CSP, `frame-ancestors`, `nosniff`, Referrer/Permissions Policy.
- Gerçek istemci IP'si yalnız güvenilen proxy zincirinden alınır.
- Upload/request body ve timeout sınırları endpoint türüne göre.
- `/metrics`, `/health/details`, Keycloak admin ve debug public route değildir.
- Caddy data/config volume yedeklenir; sertifika yenileme alarmı bulunur.

## 8. Keycloak üretim ayarları

- Production mode; açık `hostname`/proxy header yapılandırması.
- İlk bootstrap admin geçicidir; kişisel yönetici + MFA oluşturulunca kaldırılır/korumaya alınır.
- Admin console public internete açılmaz veya VPN/ayrı hostname/allowlist ile sınırlandırılır.
- Realm/client redirect URI ve web origin tam allowlist.
- Brute-force koruması, MFA politikası, session/refresh süreleri.
- Web confidential/BFF client; Android public PKCE client ayrı.
- SMTP ve event/audit ayarları; secret loglanmaz.
- Keycloak DB ve export yedeği uygulama DB'sinden ayrı doğrulanır.
- Güncelleme staging'de realm/client akışlarıyla test edilir.

## 9. PostgreSQL üretim ayarları

- Desteklenen güncel PostgreSQL major/minor; başlangıç önerisi 18'in güncel minor'u.
- Host/public port kapalı; scram ve TLS/ağ politikası.
- Runtime/migration/backup/monitoring rolleri ayrı; superuser uygulamada yok.
- `shared_buffers`, work memory, WAL/checkpoint/autovacuum connection sayısı host testine göre.
- `statement_timeout`, `lock_timeout`, idle transaction timeout uygun roller için.
- `pg_stat_statements`, yavaş sorgu ve lock monitoring.
- Sürekli WAL arşivi + pgBackRest stanza/check; arşiv başarısızlığı kritik alarm.
- DB timezone UTC; şirket yerel saati uygulama katmanında.
- OS/DB locale/collation kararı migration öncesi sabit; major değişimde test.

## 10. İlk kurulum sırası

1. Host hardening, disk/mount ve firewall.
2. Docker resmi kurulum kaynağı, sürüm sabitleme ve daemon ayarı.
3. Dizin/owner/permission oluşturma.
4. Secret ve config kontrollü yerleştirme.
5. PostgreSQL/Keycloak DB başlatma; roller.
6. pgBackRest repository/stanza ve ilk yedek doğrulaması.
7. Keycloak üretim bootstrap/realm/client/MFA.
8. API migration job; uygulama servisleri.
9. Caddy/DNS/TLS ve güvenli header testleri.
10. Observability/alert ve uzak backup.
11. Smoke, authz, golden transaction ve restore örneği.
12. Kurulum manifesti, sürüm/digest/config hash'i ve imzalı kabul.

## 11. Sürüm dağıtımı

```text
preflight → yedek/check → image pull/digest → migration precheck
→ API/web deploy → readiness → worker enable → smoke
→ gözlem → release close veya rollback
```

- Compose komutları tam proje adı/dosyasıyla çalıştırılır; yanlış dizin riski yok.
- Volume silen `down -v`, prune ve toplu delete üretim runbook'unda kullanılmaz.
- Migration ayrı tek-seferlik job ve kilit mekanizmasıyla; iki instance aynı anda çalışmaz.
- Worker DB şeması hazır olmadan başlamaz.
- Dağıtım sırasında e-fatura/ödeme belirsiz işlerinin durumu sorgulanır; kör yeniden gönderilmez.

## 12. systemd ve zamanlanmış işler

Compose stack'i boot sonrası güvenli sırada başlatan systemd unit; network-online ve mount bağımlılığı. Backup, archive verification ve bakım `systemd timer` ile; cron shell ortamına bağlı gizli davranış yok.

Timer sonuçları metrics/alert üretir. Aynı iş paralel çalışmaz; lock ve maksimum süre vardır.

## 13. Doğrulama ve arıza

Günlük/otomatik: HTTPS, cert süresi, login, API readiness, DB archive, disk, queue, backup age. Host reboot tatbikatında servisler, mount/secrets ve worker tekrarları doğru çalışmalıdır.

Arıza halinde önce veri güvenliği ve mevcut durum korunur. Rastgele container/volume silinmez. Incident açılır, korelasyon/sürüm/disk/DB/queue durumu alınır; ilgili runbook uygulanır.

## 14. Canlı kontrol listesi

- [ ] Yalnız beklenen public port/hostname açık.
- [ ] DB/metrics/admin internetten erişilemiyor.
- [ ] TLS ve security header taraması başarılı.
- [ ] Keycloak MFA, redirect, logout, break-glass testli.
- [ ] DB roller/RLS ve migration kimliği doğrulandı.
- [ ] Uzak şifreli yedek ve restore testi başarılı.
- [ ] Disk/UPS/NTP/backup/cert/queue alarmları teslim oluyor.
- [ ] Compose/image digest/config manifest saklandı.
- [ ] E-fatura/veri transferi resmi onayları belgeli.
- [ ] On-call, iş sürekliliği ve bakım penceresi duyuruldu.

## 16. Finansal servis bağımlılıkları ve start-up kapıları

Uygulama healthy sayılmadan:

- DB schema/migration version ile application compatibility;
- aktif tax/posting/e-invoice profile checksum;
- object archive read/write;
- outbox worker lease;
- NTP/UTC ve Europe/Nicosia timezone data;
- disk/WAL/archive kapasitesi;
- son backup/restore drill durumu

kontrol edilir. Dış e-Fatura/banka unavailable olabilir; bu durumda ilgili integrationStatus degraded ve queue kontrollü çalışır. Ancak DB bütünlük, RLS context veya active posting rule eksikse mali write fail-closed olur.

Posting API, worker ve reporting aynı release manifest/schema contract’ıyla uyumlu olmadan rolling karışık sürüm çalıştırılmaz. Tek host Compose deploy’da bakım penceresi ve drain:

1. yeni command kabulünü durdur;
2. in-flight transaction’ı bitir;
3. outbox lease/checkpoint’i güvenle bırak;
4. backup/restore point ve DB migration;
5. smoke + financial reconciliation;
6. write trafiğini aç

sırasını izler.

## 17. Projection ve rapor recovery

Read model/materialized view kaybı business restore gerektirmez; doğru source ledger’dan versioned rebuild job’ı çalışır. Rebuild için CPU/IO throttling, checkpoint, generation switch ve control-account/report checksum kapısı vardır. Eski generation doğrulanana kadar görünür kalır.

Canonical source, attachment/legal archive veya posted ledger kaybı ise projection rebuild ile gizlenemez; DR prosedürü ve incident başlatılır.
