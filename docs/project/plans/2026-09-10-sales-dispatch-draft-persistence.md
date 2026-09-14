# MP-04 kalıcı sevk taslağı

- **Amaç:** Siparişten yetkili depo seçimleriyle sevk taslağı kaydetmek ve aynı taslağı yeniden yüklemek; retry ve audit atomikliği sağlamak.
- **Faz / gereksinim / risk:** MP-04, SALES-DSP-004, SALES-DSP-001–003, R4.
- **Durum:** validating (iç draft persistence; runtime kanıtı bekliyor). Sahip: atanmadı. Başlangıç: 10 Eylül 2026.
- **Okunan sözleşmeler:** AGENTS, MASTER_PLAN MP-04 çıkış kapıları, docs/README, data architecture, Sales sevk ve Inventory warehouse scope, PLANS, mevcut sevk hazırlama planı.
- **DoR:** İlk sevk hazırlama loader'ı mevcut; draft persistence için yeni mali politika gerekmiyor. Gerçek dispatch posting, maliyet ve allocation ayrı kalan bağımlılıklar.

## Kapsam ve değişmezler

Kalıcı immutable draft snapshot: company/order/version, effective date, actor, request fingerprint,
order-line/item/UOM/warehouse/quantity. Tek draft'ta 1–500 unique order line, her satır tek depo.
Ürün/UOM authoritative siparişten gelir. Kaynak sipariş confirmed ve ilk-sevk hazırlığı sınırında kalır.
Taslak allocation veya stok rezervasyonu değildir; farklı taslaklar aynı miktarı hazırlayabilir,
post aşaması miktarı yeniden kilitleyip doğrulamak zorundadır. Taslaklar posted diye gösterilmez.

Permission dispatch.create + sales.order.view ve her depo için authoritative actor-bound scope.
Read aynı yetkilerle sınırlı iç servis sözleşmesidir; yeni dispatch.view permission varsayılmaz.
Modüller birbirinin tablosunu okumaz; Bootstrap Inventory-owned warehouse loader'ını bağlar.
Audit ve draft aynı transaction/savepoint'e katılır. Para, GL, maliyet, posted movement yoktur.

## Milestone / done when

1. Immutable request/result + canonical key/payload ve quantity sınırı.
2. 0051 expand migration: header/line FK, immutable guard, deferred tam satır kümesi, forced RLS, SELECT-only runtime.
3. Same-transaction writer/load + warehouse authorization callback + Bootstrap audit orchestration.
4. Unit ve gerçek DB test senaryoları, dar compile, güvenlik/diff incelemesi ve belge güncellemesi.

## Test/rollout

Retry değişmez sonuç, farklı payload conflict, sıralama normalizasyonu, source/version hatası,
warehouse/tenant/actor retleri, ikinci satır/audit rollback, eksik line commit guard ve immutable
header/line negatifleri hazırlanır. Runtime kullanıcı MP-sonu kadansında; compile başarı runtime
kanıtı değildir. OpenAPI App Control engeline dokunulmaz; bu dilimde public endpoint/SDK yoktur.
Migration otomatik uygulanmaz, eski binary + tabloları koruma rollback'idir; destructive down yok.
Bu dilim sevk/fatura/iade/GL veya MP-04 completed anlamına gelmez. Commit/push istenmedi.

## İlerleme

- 10 Eylül uygulama: immutable command/result, stable canonical fingerprint, 0051 header/line migration,
  scoped same-transaction store ve Bootstrap warehouse/audit composition tamamlandı. Inventory-owned
  warehouse loader kullanıldı; Sales başka modülün tablosunu doğrudan sorgulamaz. Audit teknik
  kimlik/kod taşır, serbest belge veya maliyet bilgisi loglanmaz. DI/public endpoint/API/SDK yoktur;
  internal PostgreSQL hata türleri henüz transport sınırı değildir. Yeni paket veya secret yoktur.
- Test hazırlığı: 500/501 sınırı, unique/mutable input, canonical sıra/decimal ve actor/date fingerprint;
  yanlış scope/depo/versiyon/miktar, audit hatasında tam rollback; immutable retry/farklı içerik ret;
  iki bağlantıda aynı key kilit beklemesi; eksik line commit constraint ve owner UPDATE reddi;
  revoked warehouse'da load/retry ret; sipariş iptalinden sonra tarihi draft retry'ı ve yeni stale draft
  reddi; sipariş sürümünün değişmemesi; forced RLS/SELECT-only catalog ve gerçek app load/başka-company
  görünmezliği. Integration fixture yalnız disposable test DB'de bir draft commit eder; production değil.
- Doğrulama: Integration Release son build 0 uyarı/0 hata (6,79 sn), Unit Release 0/0 (4,63 sn).
  İlk Bootstrap Release build 0/0 (12,95 sn). Komut biçimi `dotnet build <project> -c Release --no-restore -v:q`.
  Runtime/migration/test executable çalıştırılmadı; finansal/authorization/concurrency kanıtı MP-04
  toplu kapısında bekler. SQL deferred constraint maliyeti 500 satırda ayrıca ölçülmelidir.
- Sonraki adım: draft'tan kesinleştirmeye geçerken authoritative fulfilment/allocation, reservation
  consume ve signed stock issue/maliyet/GL'yi tek ekonomik olay zincirinde bağlamak. Hiç maliyet
  geçmişi olmayan ürün için değer uydurulmaz. Draft active-master/lot/seri/post permission kanıtı
  değildir. OpenAPI imza politikası engeli bu dar build'lerle çözülmedi. Faz completed değil; commit/push yok.
