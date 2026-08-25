# MP-03 API Idempotency Persistence Technical Spike

- **Amaç:** Finansal yazma endpointlerinin güvenebileceği tenant/company/actor/command kapsamlı PostgreSQL idempotency kaydını ve transaction-bound adaptörü oluşturmak.
- **Master fazı ve kapısı:** MP-03 / backlog 20.
- **Risk sınıfı:** R4 — duplicate mali komut, scope sızıntısı ve yanlış response replay riski.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID'leri:** `API-003`, `API-005`, `API-006`, `ACC-INV-005`.
- **Definition of Ready:** Generic persistence ve concurrency dilimi için geçer; gerçek public journal endpoint'i authoritative master-data loader ve approval sözleşmesi tamamlanana kadar kapsam dışıdır.

## Kapsam

- `platform.idempotency_record` migration, forced RLS ve minimum runtime privileges.
- Canonical request hash ile acquire/replay/conflict davranışı.
- Caller-owned transaction içinde completed response snapshot'ı.
- Aynı key paralel yarış, farklı payload, rollback ve cross-scope gerçek PostgreSQL testleri.

## Sınırlar

- Response body JSON olarak doğrulanır ve boyut sınırı uygulanır; secret/PII saklama yetkisi vermez.
- Idempotency kaydı posted journal değildir.
- Endpoint/OpenAPI wiring bu dilimde yapılmaz.
- Runtime rolü kayıt silemez; retention ayrı kontrollü operasyon olacaktır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Application sözleşmesi ve PostgreSQL migration | Build + current DB | completed |
| 2 | Transaction-bound acquire/complete adapter | Integration | completed |
| 3 | Parallel/replay/conflict/rollback/scope | Real PostgreSQL | completed |
| 4 | Full repository gate ve dokümantasyon | Full verify | completed |

## Tamamlanma kanıtı

- Solution ve standalone integration project build: 0 warning / 0 error.
- `0008` ve immutable completion hardening `0009` mevcut PostgreSQL'e uygulandı; ikinci migration koşusu 0 değişiklikle idempotent geçti.
- İlk canlı koşu parallel winner/replay noktasına ulaştı ve PostgreSQL `jsonb::text` biçim farkını ortaya çıkardı; adapter replay body'yi canonical JSON'a dönüştürecek şekilde düzeltildi.
- Canlı PostgreSQL testi parallel tek kazanan, completed response replay, farklı payload conflict, caller rollback, cross-company RLS ve minimum column privilege senaryolarıyla geçti.
- `0008` uygulanmış olduğundan değiştirilmedi; runtime UPDATE yüzeyini completion kolonlarıyla sınırlayan ve yalnız `in-progress → completed` geçişine izin veren ileri migration `0009` eklendi.
- Temiz PostgreSQL üzerinde 9 migration uygulandı; ikinci koşu 0 migration ve tüm entegrasyon kontrolleri geçti.
- Full repository verify; .NET build, 55 domain check, API/14-project architecture, web lint/typecheck/6 test/build, mevcut/boş DB, Keycloak, isolated restore ve Android kapılarından geçti.
- Auth smoke sabit `55099` yerine Windows/Docker port exclusion aralıklarıyla çakışmayan dinamik loopback port kullanır; stderr yalnız hata teşhisi için geçici dosyaya yönlendirilir ve finally bloğunda temizlenir.
