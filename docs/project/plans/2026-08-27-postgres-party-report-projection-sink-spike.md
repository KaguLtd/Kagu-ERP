# PostgreSQL Party report projection sink spike

- **Amaç:** Application job publication portunu transaction-owning PostgreSQL adapter ile mevcut atomic projection publisher'a bağlamak.
- **Master fazı:** MP-03 / backlog 18.
- **Requirement:** RPT-INV-001, RPT-INV-003, RPT-INV-005, RPT-INV-006, SEC-TEN-001.
- **Risk:** R4 — kısmi commit, RLS context eksikliği veya cross-company publication.
- **Durum:** completed.

## Sınır

Sink yalnız önceden doğrulanmış `PartyReportProjectionPublication` setini saklar. Source/policy/control-balance üretmez, scheduler veya permission code seçmez. Uygulama `NpgsqlDataSource` ve trusted `ExecutionScope` sağlar.

## Uygulama ve invariantlar

- Sink publication tenant/company değerini execution scope ile bağlantı açmadan önce doğrular.
- Tek connection ve transaction açar; tenant, actor ve izinli company listesi transaction-local PostgreSQL session context olarak kurulur.
- Canonical source watermark/checksum generation manifestine taşınır.
- Mevcut atomic publisher manifest → policy → statement → aging → subledger → GL sırasını uygular.
- Başarılı bütün setten sonra commit edilir; exception yolunda transaction dispose/rollback olur.

## Kanıt

- Release solution build 0 warning/0 error geçti.
- Migration harness yeni migration uygulamadan geçti.
- Gerçek PostgreSQL ve uygulama rolü altında ilk sink çağrısı generation setini oluşturdu.
- Aynı publication replay'i `Created=false` döndürdü ve yeni fact üretmedi.
- Başka company scope taşıyan sink `ExecutionScopeDeniedException` ile reddedildi.
- Tam PostgreSQL tenant/company RLS integration harness geçti.

Publisher defense-in-depth kontrolü statement/aging control account kimliği ile subledger/GL çiftinin kimliğini write öncesi eşleştirecek şekilde güçlendirildi. Unrelated balance fixture zero-write reddedildi. Sink ayrıca source query, Party/control hesapları, currency, cut, report definition ve generation kimliklerini connection açmadan önce tekrar eşleştirir; değiştirilmiş report code typed context mismatch üretti. Bu negatifler dahil tam PostgreSQL/RLS harness geçti.

## Açık işler

Party source, aging policy ve control-balance portlarının gerçek adapter'ları ile Worker schedule henüz bağlı değildir. Party source adapter balance-side/opening kanıtı eksikliği nedeniyle blokelidir.
