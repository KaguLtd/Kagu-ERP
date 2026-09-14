# MP-04 sevk taslağı uygulama servisi

- Amaç: Taslak oluşturma, tarihsel okuma ve güncel rezervasyon önizlemesini transaction sahibi tek uygulama sözleşmesinde toplamak.
- Master fazı: MP-04; SALES-DSP-006. Risk: R3 (scope, audit, transaction).
- Durum: validating — iç servis kodu hazır, runtime kabulü bekleniyor. Sahip: atanmadı.
  Başlangıç: 11 Eylül 2026. Son çalışma: 12 Eylül 2026.
- Okunan belgeler: AGENTS, MASTER_PLAN MP-04/DoR, docs/README, PLANS, veri mimarisi,
  Sales sevk sözleşmesi, Inventory rezervasyon/master sözleşmesi, DEC-MP01-011,
  mevcut draft/preview/master-readiness planları ve stock-order gateway kodu.
- DoR: Immutable draft store, authoritative source/master/depo sorguları ve audit
  participant'ları mevcut. Bu iş mevcut kararları bağlar; maliyet veya mevzuat kararı vermez.

## İşler ve done when

1. Sales Application: yetkili okuma sorgusu; create/load/preview gateway; Inventory/SQL
   tiplerini dışarı taşımayan exact-decimal sonuçlar ve güvenli hata sınıfları.
2. Bootstrap: ReadCommitted connection/transaction sahipliği, audit scope kontrolü,
   commit sonrası sonuç, rollback/disposal ve SQL ayrıntısını gizleyen typed hata dönüşümü.
3. Birim sözleşme ve gerçek DB gateway senaryoları; create/replay/conflict, commit edilmiş
   tekrar okuma, önizleme, şirket izolasyonu ve başarısız audit'te kalıcılık olmaması.
4. Birleşik PrepareAsync: draft create/replay + current-source/master/reservation preview
   tek transaction. Yeni taslakta sonraki kontrol hatası bütün yazımı geri alır; mevcut
   taslakta hata orijinali değiştirmez. Retry güncel hazırlığı tekrar hesaplar.

## Sınırlar

Kullanıcı tenant/actor, item/UOM veya rezervasyon sonuçlarını kendisi sağlayamaz; bunlar
trusted execution scope ve mevcut kaynaklardan gelir. Preview sonuçları stok/maliyet/GL
veya consume kanıtı değildir. Create idempotency kimliği draft ID ve canonical içeriktir;
commit bağlantı hatası belirsiz olabilir, aynı kimlikle tekrar gerekir.

Dört operasyon audit ile aynı transaction'dadır. Read işlemleri audit yüzünden DB transaction
kullanır. Production grant, owner bağlantısı fallback'i, public endpoint/DI aktivasyonu,
OpenAPI değişikliği veya migration yoktur; HTTP açılışı MP-04 kanıtını bekler.

Runtime testleri kullanıcı kadansıyla MP sonunda; dar derleme ayrı kanıt. Finansal writer,
kapalı dönem/backdate ve bilinmeyen maliyet kararları bu kapsamda açılmaz.

## İlerleme

- Dört operasyonun application sözleşmesi ve Bootstrap implementasyonu eklendi. Scope/actor
  audit doğrulaması bağlantı açılmadan yapılır; sonuçlar yalnız transaction commit'inden sonra
  döner. SQL hata içeriği taşınmaz; cancellation özgün bırakılır. Yeni bağımlılık yok.
- Birim kontrolleri: iki permission birlikte zorunlu, şirket izolasyonu, boş kimlik reddi.
- Gerçek DB gateway senaryoları hazır: runtime yetkisiyle write reddi ve owner fallback
  olmaması; yanlış audit scope; cancellation; audit SQL hatasında draft/line/audit sıfır;
  create sonrası precision hatasında birleşik rollback; commit sonrası farklı bağlantıdan
  okuma; replay/fingerprint conflict; preview exact miktar korunumu; başka şirket/görünmez ID.
- 12 Eylül ek senaryosu: mevcut taslağın ürün şirket aktivasyonu kontrollü test fixture'ında
  pasife alınır; tarihsel retry korunur, Prepare reddi önceki taslağı veya audit'i değiştirmez;
  finally ile aktivasyon geri gelir ve aynı kimlikle yeniden hazırlık yapılabilir. Bu SQL
  yalnız izole integration fixture içindedir; production'da çalıştırılmadı.
- `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release
  --no-restore -v:q`: 12 Eylül, 0 uyarı/hata, 30.66 sn. Bootstrap kaynakları bu kontrol
  projesinin mevcut derleme kapsamındadır. 11 Eylül doğrudan Bootstrap ilk derlemesi
  0 uyarı/hata (5.10 sn) idi; daha sonraki değişikliklerin kanıtı integration derlemesidir.
- Önceki oturumun son process ID'si devamda bulunamadığı için onun sonucu başarı sayılmadı;
  `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj -c Release --no-restore -v:q`
  12 Eylül son derlemesi de 0 uyarı/hata, 16.35 sn ile tamamlandı.
  güncel kodla dar integration derlemesi tekrarlandı. `git diff --check` temiz.
- Runtime/DB/finansal ve yetki testleri kullanıcı MP-sonu kadansı nedeniyle çalıştırılmadı.
  Transaction/RLS/atomiklik sonuçları henüz kanıtlanmış değildir. API/OpenAPI CodeIntegrity
  engeli açık; yeniden denenmedi ve bypass edilmedi. HTTP/DI ve write grant açılmadı.
- Master kapısı değişmedi. Commit/push yok. Sonraki uygulama adımı hazırlık değil gerçek
  sevk/allocation/consume ve maliyet/GL atomikliğidir; DEC-011'in bilinmeyen maliyet ve
  geriye tarihli uzlaştırma ayrıntıları varsayımla kapatılmayacaktır.
