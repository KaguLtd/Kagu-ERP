# MP-04 maliyet yayını kalıcılık işlemi

- Amaç: Hazırlanmış history evidence'ını 0052 tablosuna idempotent ve audit ile atomik kaydetmek.
- Faz MP-04 / INV-COST-004; risk R3; sahip atanmadı; durum validating (runtime kabulü bekliyor).
- Ready: INV-COST-001/002 evidence + immutable tablo sözleşmesi var. DEC-011 maliyet
  politikası değişmez. Okunanlar: AGENTS, MASTER_PLAN MP-04, docs/README rotası,
  Inventory maliyet, veri mimarisi, 0052 ve cost history/preview planları.

## Kapsam ve done when

Inventory-owned writer: scope + inventory.cost.publish + güncel depo kapsamı; publication
kimliğine transaction advisory lock; exact içerik/actor replay; başka içerik conflict.
Savepoint insert/unique failure sonrası transaction'ı kullanılabilir tutar. Bootstrap audit
aynı transaction'dadır; audit hatası yeni publication'ı da geri alır. Testler owner fixture ile
hazırlanır; runtime uygulama grant'i kapalı kalır.

## Sınırlar

Bu writer faturayı doğrulayan valuation producer DEĞİLDİR. Doğru/latest invoice-derived
snapshot ve gerçek NoHistory kararı upstream producer'ın sorumluluğudur; o producer henüz yok.
HTTP/DI ya da kullanıcı input'u bu metoda bağlanmaz. Yeni permission kullanıcıya atanmaz;
runtime INSERT yetkisi açılmaz. Muhasebe, fatura veya fiziksel stok kaydı oluşturulmaz.
Migration ve yeni bağımlılık yok; mevcut kaydı UPDATE/DELETE yok. Read-only tarihsel replay
eski immutable sonucu korur; bugünkü master pasifliği geçmiş sonucu değiştirmez.

## Doğrulama

Create/replay/content conflict, aynı watermark farklı identity, aktör/depo scope,
audit failure rollback, explicit zero ve app write denial; MP-sonunda runtime.
Dar derleme ayrı kanıt. MP kapısı ve GL kabulü bu iç writer ile tamamlanmış sayılmaz.

## Sonuç / kanıt

- Inventory writer ve Bootstrap audit participant eklendi. Same-key lock, exact replay ve
  watermark unique conflict kontrollü; SQL failure rollback sonrası transaction kullanılabilir.
  Requested ve persisted depo kapsamı kontrolü var. Timestamp DB'den UTC typed olarak okunur.
- Integration senaryoları create/replay/changed cost/duplicate watermark, no-history nullable
  snapshot round-trip, wrong actor, missing publish permission, audit SQL failure rollback,
  runtime write denial, ikinci bağlantıda lock_timeout ve writer→reader→issue-cost-preview
  zincirini kapsayacak biçimde eklendi. Owner test transaction'ı sonunda rollback edilir.
- Son `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release
  --no-restore -v:q`: 0 uyarı/hata, 18.23 sn.
- `dotnet build src/Erp.Bootstrap/KaguERP.Bootstrap.csproj -c Release --no-restore -v:q`:
  0 uyarı/hata, 4.53 sn. `git diff --check` temiz.
- Runtime/DB/finansal/güvenlik senaryoları kullanıcı MP-sonu kadansı nedeniyle çalıştırılmadı.
  Derleme SQL, kilit ve audit atomikliği kanıtı değildir. Yeni migration yok; 0052 henüz
  uygulanmadı. CodeIntegrity/API/OpenAPI engeline müdahale edilmedi.
- Invoice/valuation evidence producer hâlâ yok; yalnız kalıcılık participant'ı hazırlandı.
  Gerçek sevk, GL, fatura doğrulaması veya otomatik maliyet hesaplama tamamlanmadı.
  Master kapısı değişmedi; commit/push yapılmadı.
