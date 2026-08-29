# Party statement drill-down anchor spike

- **Amaç:** Persisted cari ekstre satırından kaynak modül resolver'ına geçiş için exact projection-generation bağlı authoritative anchor üretmek.
- **Master fazı:** MP-03 / backlog 18 drill-down ön koşulu; backlog 19 web akışına hazırlık.
- **Requirement:** RPT-INV-001, RPT-INV-002, RPT-PARTY-001, SEC-TEN-001.
- **Risk:** R4 — farklı veri kesiminden kaynak satır bağlama veya şirketler arası metadata sızıntısı.
- **Durum:** completed.

## Sınır

Bu dilim public endpoint, UI route, production permission code veya source-document resolver tanımlamaz. Yalnız Reporting-owned immutable statement projection'ından statement/event kaynak lineage'ını aynı report slice ile döndürür.

## Kanıt

- Domain anchor statement ID, full financial report slice, normalized event snapshot ve running exposure taşır.
- Event tenant/company/currency/effective-as-of/recorded-cutoff bağlamı anchor oluşturulurken yeniden doğrulanır.
- PostgreSQL loader company scope, projection generation, statement ID ve event ID'yi birlikte ister.
- Exact fixture source type/event ID, due-schedule line ve running exposure ile round-trip edildi.
- Yanlış generation aynı statement/event satırını yeniden kullanamadı ve `null` döndü.
- Başka company scope aynı kimliklerin varlığını göremedi.
- Release build 0 warning/0 error ve tam PostgreSQL/RLS integration harness geçti.

## Sıradaki adım

Anchor'ın `SourceType` değerine göre yayımlanmış source-module contract'ına giden permission-preserving resolver portu tanımlanacaktır. Gerçek belge route'u ve permission kodu MP-01 kararları olmadan seçilmeyecektir.
