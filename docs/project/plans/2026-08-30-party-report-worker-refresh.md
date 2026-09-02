# Party report durable Worker refresh

- **Amaç:** Cari ekstre/aging/GL projection job'unu insan hesabını taklit etmeden, açık tenant/company kapsamlı ve crash sonrası güvenle tekrar çalışabilen PostgreSQL-backed Worker ile yürütmek.
- **Master fazı ve kapısı:** MP-03 / backlog 18, kaynak→rapor golden zincirinin operasyonel yenileme kanıtı.
- **Risk sınıfı:** R4 — yanlış scope ile mali projection yayımlama, çift çalışma veya crash sonrası iş kaybı.
- **Durum:** validating.
- **Sahip:** KaguLtd repository sahibi; isimli ürün/güvenlik/teknik sahipler `DEC-MP01-019` gereği atanmadı.
- **Başlangıç / hedef tarih:** 30 Ağustos 2026 / aynı dikey dilim.
- **İlgili requirement ID'leri:** IAM-POL-002, RPT-OPS-001, RPT-INV-001, RPT-INV-005, RPT-INV-006, RPT-PARTY-001, RPT-PARTY-002, RPT-CTRL-001, SEC-TEN-001.
- **Etkilenen belgeler/modüller:** IAM, Reporting Application/Infrastructure, Bootstrap, Worker, Migrator ve gerçek PostgreSQL integration hostu.
- **Okunan zorunlu belgeler:** `AGENTS.md`, `MASTER_PLAN.md`, `docs/README.md`, `docs/00-foundation/00-technical-foundation.md`, `04-data-architecture.md`, `07-cross-cutting-workflows.md`, `docs/modules/01-identity-access.md`, `14-reporting-dashboard.md`, `docs/quality/01-testing-and-quality-strategy.md`, Reporting `AGENTS.md` ve mevcut projection-job planı/kodu.
- **Definition of Ready sonucu:** `DEC-MP01-020` servis kimliğini insan kimliğinden ayırdı. Açılış-aging ürün kararı bu dilimin yalnız due-source raporu işlemesini engellemiyor. Production secret/kimlik provisioning kapsam dışıdır.

## Master plan ilişkisi

MP-03'te source, policy, exact Party→GL evidence, atomic sink ve production query hazırdır; eksik operasyonel bağ Worker scheduling idi. Bu plan tekrar eden schedule authoring ekranını değil, açık schedule occurrence/work-item kaydını dayanıklı ve idempotent biçimde işleyen ilk güvenli Worker dilimini kapatır.

## Bağlam

`PartyReportProjectionJob` provider-independent ve fail-closed çalışır, fakat bugün yalnız test kompozisyonundan çağrılır. `Erp.Worker` generic host olarak ayağa kalkar; servis kimliği, company kapsamı, durable claim/lease/retry ve production composition yoktur.

## Kapsam

### Dahil

- İnsan hesabından ayrı, DB-backed aktif service identity ve company permission kanıtı.
- Deployment tenant/actor/company allow-list'indeki her şirketin IAM izniyle doğrulanması; IAM'deki deployment dışı izinlerin etkin kapsama alınmaması.
- `reporting.party-account.refresh` permission'ı.
- Tenant/company scoped, idempotency-key'li durable refresh work item.
- `FOR UPDATE SKIP LOCKED` claim, süreli lease, bounded retry ve append-only iş olay izi.
- Existing Party source → aging policy → exact GL evidence → atomic sink production composition.
- Worker restart/lease expiry, iki worker yarışması, cross-company ve revoked identity negatif testleri.

### Dahil değil

- Recurring schedule authoring UI/API, cron parser veya business-calendar tatil kataloğu.
- Production Keycloak client/secret oluşturma veya secret rotation.
- Opening balance'ı aging open item'a dönüştürme.
- Web/Android rapor contract uyarlaması, export ve bildirim.

## Değişmezler ve güvenlik sınırları

- Finansal invariant: Worker yalnız mevcut application job'u çağırır; source/statement/aging ve Party→GL exact cross-foot geçmeden publication yoktur.
- Yetki/scope: Sınırsız `system` context yoktur. Her claim tek tenant ve deployment+IAM company allow-list kesişimindedir; publish öncesi kimlik yeniden doğrulanır.
- Kişisel veri: Queue PII, cari adı veya belge içeriği taşımaz; yalnız opaque ID, kesim ve güvenli hata kodu saklar.
- Mevzuat: Yeni vergi, kur veya resmi belge kuralı seçmez.
- Geriye uyumluluk: Yalnız expand migration; mevcut rapor/API kayıtları değişmez.
- Veri kaybı riski: Projection facts immutable/idempotent; queue operational state'i lease ile geri alınabilir ve her geçiş append-only event taşır.

## Tasarım

- İş süreci: schedule/manual producer → idempotent enqueue → Worker service identity doğrulama → atomic claim → existing projection job → complete; exception'da bounded retry/terminal failure; crash'te lease expiry sonrası reclaim.
- REA özeti: Yeni ekonomik olay yoktur. Work item ve event yalnız türetilmiş reporting projection üretiminin operasyon kanıtıdır.
- Domain değişikliği: Finansal domain değişmez; Application queue/processor portu eklenir.
- Veritabanı/migration: IAM service identity + company permission; Reporting work item + append-only event; forced RLS, narrow grants ve transition trigger.
- API ve olaylar: Public endpoint yoktur. Work item report version, effective as-of, UTC cutoff, timezone, business calendar ve missed-run policy taşır.
- Audit/observability: claim/completed/retry/failed eventleri actor, attempt ve güvenli error code ile append-only saklanır; loglar opaque scope taşır.
- Deployment/rollback: Worker ayarlarının tamamı yoksa refresh consumer kapalıdır; kısmi ayar startup'ı fail-closed durdurur. Migration expand-only kalır.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | `DEC-MP01-020` ve servis kimliği sözleşmesi | Karar/IAM dokümanı | completed |
| 2 | IAM ve durable queue migration'ı | Boş/mevcut DB, RLS, grant ve transition negatifleri | completed |
| 3 | Queue/application processor + production composition | Build, architecture ve unit contract | completed |
| 4 | Worker hosted loop + config fail-closed | Host/config smoke | completed |
| 5 | Gerçek PostgreSQL concurrency/restart/revocation/golden | Integration ve migration tekrar | validating |
| 6 | Repository kapıları ve master kanıtı | Build/test/format/diff/docs | in-progress |

## Test planı

- Unit: request doğrulama, retry sonucu ve config parsing.
- DB integration: idempotent enqueue, payload conflict, iki claimant tek kazanan, expired lease reclaim, complete/retry/terminal eventleri.
- Security: başka company görünmez; IAM veya deployment scope eksikliği claim etmez; inactive/revoked identity publish etmez.
- Migration: mevcut DB yükseltme, sıfırdan DB ve tekrar 0/0.
- Golden accounting/report: Mevcut posted due fixture'ı gerçek Worker processor üzerinden statement/aging/GL sıfır farkla yayınlanır; replay yeni projection oluşturmaz.
- Fault: claim sonrası process ölümü lease expiry ile güvenli tekrar; sink publication mevcut idempotent generation anahtarlarını korur.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-08-30 | İnsan kullanıcı hesabı Worker'da kullanılırsa yetki iptali ve audit semantiği bozulur | Yetkisiz arka plan yayını | `DEC-MP01-020`, ayrı service identity |
| 2026-08-30 | DB'deki service scope deployment hatasıyla genişleyebilir | Company kapsam aşımı | İki taraflı allow-list; biri eksikse startup/claim fail-closed |
| 2026-08-30 | Claim sonrası crash işi belirsiz bırakabilir | Kayıp veya çift refresh | Lease expiry + same deterministic generation ile idempotent reclaim |
| 2026-08-31 | Statement event tekilliği tenant genelindeydi | Aynı kaynak kesimi ikinci immutable generation'da yayımlanamıyordu | `0040` ile tekillik `(tenant, company, statement, event)` kapsamına alındı |

## İlerleme günlüğü

### 2026-08-30

- Mevcut Worker, IAM, RLS, projection job/source/sink ve schedule sözleşmeleri okundu.
- Kullanıcının güvenli öneri sonrası devam talimatı `DEC-MP01-020` olarak kaydedildi.
- Durable work-item dikey dilimi recurring schedule authoring'den ayrıldı; schedule occurrence timezone/business-calendar/missed-run bağlamını açık taşıyacak.

### 2026-08-31

- `0038`, kullanıcıdan ayrık active service identity ve tarih etkili company permission kayıtlarını forced RLS altında ekledi. User/service ID çakışması iki yönde DB trigger'ıyla reddediliyor.
- `0039`, idempotent Party refresh work item'ı, append-only transition eventleri, lease/reclaim, bounded attempts ve daraltılmış runtime grantleri ekledi.
- Application processor, PostgreSQL `SKIP LOCKED` store, Bootstrap production source/policy/exact-GL/sink composition ve Worker hosted loop tamamlandı. Ayarlar bütünüyle yoksa consumer kapalı; kısmi ayar startup'ı fail-closed durduruyor.
- Gerçek DB testinde idempotent enqueue/payload conflict, iki claimant tek kazanan, retry/terminal failure, expired lease reclaim, last-attempt crash terminalization, cross-company görünmezlik, overbroad/pasif service identity ve queue tamper negatifleri geçti.
- Production Worker golden ilk koşuda güvenli retry üretti. PostgreSQL günlüğü eski `uq_party_statement_projection_event (tenant_id,event_id)` kısıtının aynı source event'i ikinci immutable generation'da yasakladığını gösterdi. Uygulanmış `0026` değiştirilmedi; `0040` ileri migration'ı constraint'i statement kapsamına aldı.
- `0040` mevcut yerel DB'ye checksum kaydıyla uygulandı ve catalog tanımı `UNIQUE (tenant_id, company_id, statement_id, event_id)` olarak doğrulandı. Release solution build `0 warning/error` geçti.
- `0040` sonrası tam C# runtime tekrarı Windows Application Control tarafından `0x800711C7` ile engelleniyor. Politika zayıflatılmadı veya bypass edilmedi; Worker golden, boş DB ve son 0/0 tekrar kanıtları bu ortam kapısı açılana kadar validating kalır.

## Tamamlanma kanıtı

- [x] Kabul kriterleri ve bounded retry/lease akışı.
- [x] Test komutları ve ara sonuçları.
- [ ] Migration boş/mevcut DB ve 0/0 kanıtı.
- [x] Güvenlik, revoke ve tenant/company negatif testleri.
- [x] Doküman güncellemesi.
- [ ] Projection replay ve sıfır fark kanıtı.
- [x] Production provisioning açık işi kayıtlı.
- [x] MASTER_PLAN.md etkisi değerlendirildi.
