# Party report production API query

- **Amaç:** Persisted cari ekstre + yaşlandırma cross-foot'unu production permission, company scope ve append-only audit ile `/api/v1` üzerinden güvenle sorgulamak.
- **Master fazı:** MP-03 / backlog 18–19.
- **Requirement:** IAM-INV-002, RPT-INV-001, RPT-INV-002, RPT-PARTY-001, RPT-PARTY-002, SEC-TEN-001, DEC-MP01-012.
- **Risk:** R4 — yetkisiz rapor/şirket verisi sızıntısı, route ile projection tanımının karışması veya audit yazılmadan mali veri dönmesi.
- **Durum:** completed.

## Karar ve sınır

Production cari raporu `party.account.detail` / definition version `1` ve ayrı `reporting.party-account.view` permission kodunu kullanır. Permission kodu değişmez API sözleşmesidir; export, print, maliyet ve marj ayrı permission'dır. Endpoint yalnız daha önce atomik üretilmiş statement/aging projection çiftini sorgular; yeni projection üretmez ve source modül tablolarını okumaz.

`POST /api/v1/reports/{code}/queries` gövdesi `companyId`, `statementId` ve `agingReportId` alır. Cross-foot ve audit kimlikleri server tarafından üretilir. Permission ve company scope resource lookup'tan önce denetlenir; denied/not-found/allowed sonuçları aynı PostgreSQL transaction'ında append-only audit'e yazılmadan response dönmez.

## Milestone'lar

| # | Milestone | Done when | Durum |
|---:|---|---|---|
| 1 | Production report definition | Code/version/permission immutable application sözleşmesidir | completed |
| 2 | Transaction-owning query executor | RLS context + permission-first load + audit tek transaction'dadır | completed |
| 3 | `/api/v1` endpoint ve DTO | Route/body validation, safe Problem Details ve explicit response vardır | completed |
| 4 | Negatif ve PostgreSQL kanıtı | Missing permission, wrong company, wrong definition ve audit failure fail-closed geçer | completed |
| 5 | Repository kapıları ve belgeler | Release build/test/format/DB ve sözleşmeler günceldir | completed |

## Tamamlama kanıtı

- API yalnız Reporting Application query sözleşmesine bağlıdır; PostgreSQL adapter ve transaction-bound ortak audit appender Bootstrap composition root'ta bağlanır. API'nin Reporting Domain/Infrastructure'a doğrudan başvurusu mimari kapıyla reddedilmiş ve kaldırılmıştır.
- `party.account.detail` version `1` tanımı yalnız `reporting.party-account.view` ile açılır. Company scope ve permission, statement/aging kimlikleri yüklenmeden önce denetlenir; yanlış report definition version'ı görünmez kalır.
- Allowed, denied ve not-found audit olayları projection okumasıyla aynı PostgreSQL transaction'ında commit edilir. Denied olay target ID taşımaz; audit hatası başarılı mali veri yanıtına dönüşmez.
- Endpoint yalnız persisted immutable statement/aging cross-foot'unu döndürür. Route/body hataları güvenli Problem Details; yetki reddi `403`, bulunamayan projection `404`, altyapı yokluğu veya güvenli hata `503` üretir.
- Response'taki parasal alanlar tarayıcıda binary floating-point dönüşümüne uğramaması için invariant dört ondalıklı JSON string'idir. Statement kind ve balance-side değerleri C# enum adından türetilmez; version `1` için explicit kararlı kodlardır.
- Release solution build `0 warning/error`; domain host `63` check; architecture/API host `20` source project; `dotnet format --verify-no-changes`, `git diff --check` ve gerçek PostgreSQL tenant/company RLS integration paketi geçti. Migration tekrarı iki kez `0/0` kaldı.
- Ortak audit appender'ının son DI-only composition refaktörü 0/0 derlendi ve kilitli restore geçti. Bu son rebuild sonrasında Windows Application Control hem yeniden üretilmiş Architecture hem standalone Integration DLL'ini `0x800711C7` ile engelledi; güvenlik politikası değiştirilmedi. Aynı turda refaktör öncesi production query'nin allowed/denied/not-found PostgreSQL paketi iki kez geçmişti; ortak appender'ın mevcut audit testleri de daha önce yeşildi. Son birleşik runtime tekrarının bu ortam engeli riski teslim notunda açık tutulur.

## Kapsam dışı

- Projection refresh/Worker schedule.
- Rapor kataloğu, export, print ve web ekranı.
- Açılış bakiyesini due/open-item sayma kararı.
- Production kullanıcılarına permission atama ve isimli access-review sahibi.

## Rollback / compensation

Yeni route deploy'dan çıkarılabilir; persisted projection ve audit fact'leri silinmez. Permission ataması otomatik seed edilmez, dolayısıyla route açık olsa bile açıkça yetki verilmemiş kullanıcı fail-closed kalır.
