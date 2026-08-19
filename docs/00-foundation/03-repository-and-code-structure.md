# 03 — Repository ve Kod Yapısı

## Hedef monorepo

```text
/
├── AGENTS.md
├── MASTER_PLAN.md
├── README.md
├── PLANS.md
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── package.json
├── pnpm-workspace.yaml
├── compose.yaml
├── compose.dev.yaml
├── compose.prod.yaml
├── .env.example
├── .editorconfig
├── .gitattributes
├── .gitignore
├── apps/
│   ├── web/
│   │   ├── AGENTS.md
│   │   ├── src/{app,components,features,lib,routes,styles,test}/
│   │   └── e2e/
│   └── android/
│       ├── AGENTS.md
│       ├── app/
│       ├── core/{common,designsystem,network,database,auth,testing}/
│       └── feature/{dashboard,party,inventory,approvals,instruments}/
├── src/
│   ├── Erp.Api/
│   ├── Erp.Worker/
│   ├── Erp.Bootstrap/
│   ├── BuildingBlocks/{Domain,Application,Infrastructure,Contracts}/
│   └── Modules/
│       ├── Identity/
│       ├── Organization/
│       ├── Parties/
│       ├── Inventory/
│       ├── Sales/
│       ├── Purchasing/
│       ├── Treasury/
│       ├── Instruments/
│       ├── Accounting/
│       ├── Compliance/
│       ├── Workflow/
│       ├── Documents/
│       ├── Reporting/
│       └── Integrations/
├── tests/
│   ├── Unit/
│   ├── Architecture/
│   ├── Integration/
│   ├── Contract/
│   ├── EndToEnd/
│   ├── Security/
│   ├── Performance/
│   └── Fixtures/
├── packages/
│   ├── api-client-ts/
│   ├── api-client-kotlin/
│   ├── design-tokens/
│   └── test-data-builders/
├── db/
│   ├── migrations/
│   ├── seed/reference/
│   ├── seed/demo/
│   ├── rls/
│   └── verification/
├── deploy/
│   ├── caddy/
│   ├── docker/
│   ├── keycloak/
│   ├── observability/
│   ├── backup/
│   ├── systemd/
│   └── runbooks/
├── docs/
├── scripts/
│   ├── bootstrap.sh
│   ├── verify.sh
│   ├── test.sh
│   ├── migrate.sh
│   ├── backup-check.sh
│   └── restore-drill.sh
└── artifacts/                 # gitignored test/build çıktısı
```

## Backend modül şablonu

```text
src/Modules/Sales/
├── AGENTS.md
├── Sales.Domain/
│   ├── Orders/
│   ├── Invoices/
│   ├── ValueObjects/
│   └── Events/
├── Sales.Application/
│   ├── Commands/
│   ├── Queries/
│   ├── Policies/
│   └── Ports/
├── Sales.Infrastructure/
│   ├── Persistence/
│   ├── Configurations/
│   └── Adapters/
├── Sales.Contracts/
│   ├── Dtos/
│   ├── Events/
│   └── Interfaces/
└── Sales.Api/
    ├── Endpoints/
    └── Mapping/
```

### Bağımlılık yönü

`Domain ← Application ← Api/Infrastructure`; `Contracts` dış dünyaya açık dar yüzeydir. Domain, EF Core, HTTP, Keycloak veya dosya sistemini bilmez.

## Web feature şablonu

```text
apps/web/src/features/invoices/
├── api/                 # generated client wrapper ve query keys
├── components/          # feature bileşenleri
├── routes/              # liste/detay/düzenleme route'ları
├── schemas/             # Zod/form şemaları; server kuralının kopyası değil
├── hooks/
├── model/               # UI-only model ve mapping
├── tests/
└── index.ts             # kontrollü public exports
```

Feature başka feature'ın iç dosyasına import yapmaz. Paylaşılan saf UI `components/ui`; iş bileşenleri feature içinde kalır.

## Android modül şablonu

```text
feature/party/
├── api/
├── data/{local,remote,repository}/
├── domain/{model,usecase}/
├── ui/{list,detail,statement}/
└── test/
```

UI yalnız repository/use case üzerinden veriye erişir. Network DTO ve Room entity dış katmana sızmaz.

## Dosya adlandırma

- C# türü ve dosyası PascalCase; bir public tür/ana dosya.
- TypeScript component PascalCase; hook `useX`; route klasörleri lowercase-kebab.
- Kotlin türleri PascalCase; paketler lowercase.
- SQL schema/table/column `snake_case`.
- API path çoğul lowercase-kebab; JSON alanları `camelCase`.
- Requirement ve event isimleri sabit, sürümlü ve İngilizce kod adı taşır.

## Package ve dependency yönetimi

- .NET merkezi sürüm: `Directory.Packages.props`.
- SDK `global.json` ile pinlenir; roll-forward politikası açık.
- JS package manager `pnpm`; lockfile commit edilir; CI `--frozen-lockfile`.
- Android version catalog `libs.versions.toml`; dependency verification etkin.
- Container image digestleri release manifestinde.
- Otomatik major dependency update merge edilmez; migration/release note incelemesi gerekir.

## Generated ve source dosyalar

- OpenAPI çıktıları `packages/api-client-*` altında generated etiketiyle; elle değiştirilmez.
- Migration generated başlangıç olabilir fakat SQL ve lock etkisi insan/Codex review'dan geçer.
- shadcn/ui componentleri repository içine kopyalandığı için source kabul edilir; doğrudan değiştirilebilir fakat tasarım tokenlarına uyulur.
- UBL/XSD/Schematron resmi kaynakları checksum ve kaynak tarihiyle `db/seed/reference` veya ayrı `reference-data` paketinde tutulur; lisans/dağıtım uygunluğu doğrulanır.

## Mimari testler

- Domain projeleri Infrastructure/Api referansı alamaz.
- Bir modül başka modülün Infrastructure projesini referanslayamaz.
- `Internal` domain tipleri dış modüle sızamaz.
- Controller/endpoint içinde doğrudan `DbContext` kullanılamaz.
- Web feature internal import yasağı lint ile.
- Android UI network/DAO tipini referanslayamaz.

## Repository bootstrap kabulü

- [ ] Root `AGENTS.md`, `MASTER_PLAN.md`, `README.md` ve `PLANS.md` birlikte taşınmış; aralarındaki linkler geçerli.
- [ ] Tüm klasörler ve nested `AGENTS.md` dosyaları oluşturulmuş.
- [ ] `scripts/bootstrap.sh` ve `scripts/verify.sh` temiz makinede çalışıyor.
- [ ] Lock dosyaları ve SDK sürümleri pinli.
- [ ] Örnek secret yok; yalnız `.env.example` anahtar isimleri.
- [ ] Architecture testleri CI'da.
- [ ] Dev Compose sentetik seed ile açılıyor.

## 12. v1.1 domain ve kanıt yerleşimi

Mevcut klasör yapısını bozmadan her modül aşağıdaki sorumlulukları kendi katmanında tutar:

- Domain: commitment, economic event, state machine ve invariantlar.
- Application: command/query, authorization, posting orchestration ve exception sonucu.
- Infrastructure: repository, outbox/inbox, provider adapter ve projection writer.
- Contracts: API DTO, domain event, PostingRequest ve report manifest.
- Tests: scenario/golden data, property test, authorization matrix ve reconciliation oracle.

Yeni bir modülün içinde yalnız “Entities” ve CRUD controller açmak yeterli değildir. En az şu isimlendirilmiş artefaktlar bulunur:

| Artefakt | İçerik |
|---|---|
| ProcessContract | aktör, ön koşul, happy path, exception, telafi |
| AccountingScenario | kaynak olay, alt defter/GL beklenen satırları ve reversal |
| ControlCatalog | risk, owner, kontrol, kanıt ve exception |
| ReportDefinition | as-of, filtre, kolon, toplam, drill-down ve yetki |
| MigrationReconciliation | kaynak/hedef sayım, tutar, hash ve açık fark |

Test fixture’ları iş örneklerini sabit para/tarih/kur/kural snapshot’ıyla kurar. Golden sonuç elle saklanan belirsiz CSV değil, muhasebe sahibince incelenmiş sürümlü senaryodur.

## 13. Sınır ve bağımlılık testleri

- Sales/Purchasing doğrudan Accounting repository’sine referans veremez; yalnız application contract.
- Reporting kaynak modüle yazamaz ve cache’i iş gerçeği yapamaz.
- Treasury Payment kaydı, Party PaymentAllocation satırını doğrudan değiştirmez; allocation command kullanır.
- Projection rebuild kodu normal business command handler’ını taklit ederek yeni dış olay veya numara üretmez.
- Provider/UBL/ISO 20022 modelleri domain entity değildir; anti-corruption mapping ve raw payload hash’i ile ayrılır.
- Architecture testleri bu dependency yönlerini, yasak DB erişimini ve cross-schema write’ı CI’da bloklar.
