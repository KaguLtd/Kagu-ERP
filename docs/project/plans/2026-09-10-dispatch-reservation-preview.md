# MP-04 sevk–rezervasyon bağlantısı

- **Amaç:** Kalıcı sevk taslağının her satırını doğru sipariş/depo rezervasyonlarıyla eşleştirmek; rezervasyonla karşılanan ve karşılanmayan miktarı ayrı göstermek.
- **Faz/risk:** MP-04 / R4; SALES-DSP-005, INV-RES-015.
- **Durum:** validating — iç uygulama ve senaryolar hazır; runtime kabulü bekliyor. Sahip: atanmadı. Başlangıç: 10 Eylül 2026.
- **Okunan belgeler:** MASTER_PLAN MP-04, AGENTS, docs/README, Sales sevk, Inventory lifecycle/kilit, data architecture, mevcut kalıcı sevk taslağı planı.
- **DoR:** Draft/current order ve reservation creation/lifecycle kaynakları var. Finansal kesinleştirme hâlâ maliyet/GL ve dönem bağımlıdır; bu iş bunu atlamaz.

## Tasarım / done when

1. Exact decimal tüketim dağılımı: rezervasyon başına kalanı aşmaz; toplam karşılanan + rezervasyonsuz miktar = sevk miktarı. Karşılanmayan miktar eksi stokla aynı şey değildir; elde serbest stok da bulunabilir.
2. Inventory-owned query doğru source/order-line/version, item/UOM ve warehouse'u doğrular. Önce tüm demand, sonra tüm position kilitleri sıralı alınır; depo kapsamı tekrar doğrulanır. Tarihsel lifecycle'a geri dönülmüş tüketim planı sessiz üretilmez.
3. Bootstrap draft ve güncel source doğrulamasını Inventory okumasına bağlar. Company/actor-bound yetki, immutable sonuç ve read audit aynı caller transaction'dadır.
4. Unit ve gerçek DB senaryolarını hazırla; runtime kullanıcı MP-sonu kadansında, dar compile ayrı kanıttır.

Deterministik seçim mevcut creation recorded time + reservation ID sırasındadır; bu maliyet FIFO
yöntemi seçmek değildir. Sonuç expected reservation version taşır; ileride tüketim writer'ı yeniden
doğrulamak zorundadır. Query rezervasyon tüketmez, stock movement/allocation/GL veya posted dispatch
yaratmaz. Public endpoint, SQL grant, migration ve yeni permission yoktur. Mevcut dispatch.create +
sales.order.view ile sınırlı iç hazırlık yoludur. Eksi stok izni ve son bilinen maliyet kararı korunur.

## İlerleme

- 11 Eylül: Güncel master readiness guard ve pasif ürün/şirket/depo, ölçek, tarihsel
  okuma/audit rollback senaryoları eklendi. Ayrıntı ve yeni derleme kanıtı
  [stok kartı hazırlık planında](2026-09-11-dispatch-stock-master-readiness.md). Runtime kabulü bekliyor.

- Domain exact-decimal dağılımı, immutable application sorgusu, Inventory-owned kilitli okuma
  ve Bootstrap current-source/audit bağlantısı eklendi. En fazla 500 satır ve toplam 500
  rezervasyon başlığı desteklenir; tarih/pozisyon uyuşmazlığı fail-closed'dur.
- Unit senaryoları: deterministik dağılım, sıfır/eksik rezervasyon, korunum, kapasite,
  snapshot, yinelenen/geçersiz/sınır aşan girdi.
- Gerçek PostgreSQL fixture senaryoları hazır: iki satırın ayrı rezervasyonları, release
  sonrası kalan, gelecekteki release, eski tarih, yanlış item/depo, eksik permission,
  ilgisiz kaynak satırı, iptal edilmiş kaynak, audit hatasında rollback ve salt-okuma
  lifecycle sayacı. Mevcut batch fixture içinde savepoint ile temizlenir.
- Dar derleme kanıtı (10 Eylül): `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj
  -c Release --no-restore -v:q` 0 uyarı/hata, 2.97 sn;
  `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release
  --no-restore -v:q` son çalıştırma 0 uyarı/hata, 5.67 sn;
  `dotnet build src/Erp.Bootstrap/KaguERP.Bootstrap.csproj -c Release --no-restore -v:q`
  son çalıştırma 0 uyarı/hata, 2.47 sn. İlk integration komutu yanlış proje adı nedeniyle
  MSB1009 döndü; doğru repository yolu ile tekrarlandı.
- Runtime/gerçek DB/finansal ve güvenlik negatif testleri bu oturumda çalıştırılmadı:
  kullanıcının MP-sonu toplu test kadansı korunuyor. Derleme davranış kanıtı değildir;
  gerçek DB kilit, RLS, migration ve audit atomikliği MP kapısında doğrulanmalıdır.
  Mevcut Windows CodeIntegrity API/OpenAPI çalıştırma engeli ayrıca açık; bypass yapılmadı.
- Finansal kesinleştirme, rezervasyon tüketimi ve stok/GL writer'ı tamamlanmadı; yeni
  grant, endpoint, migration veya production değişikliği yok. Master kapısı değişmedi;
  commit/push yok. Sonraki iş gerçek sevk kaynak/fulfillment ve stok çıkışı atomik sınırını
  mevcut maliyet/dönem sözleşmeleriyle birleştirmek; önizlemeyi posting sonucu saymamak.
