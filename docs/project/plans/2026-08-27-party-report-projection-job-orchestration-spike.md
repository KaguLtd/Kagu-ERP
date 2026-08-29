# Party report projection job orchestration spike

- **Amaç:** Party source, aging policy, control-account evidence ve atomic projection publication adımlarını provider-independent, fail-closed bir application job sınırında birleştirmek.
- **Master fazı:** MP-03 / backlog 18.
- **Requirement:** RPT-INV-001, RPT-PARTY-001, RPT-PARTY-002, RPT-CTRL-001, SEC-TEN-001.
- **Risk:** R4 — yanlış scope/cut veya control account ile projection yayımlama.
- **Durum:** completed.

## Sınır

Job scheduler, PostgreSQL transaction, firma aging policy seçimi, source adapter, permission code veya public API tanımlamaz. `IPartyReportSource`, `IPartyAgingPolicySource`, `IPartyControlAccountEvidenceSource` ve `IPartyReportProjectionSink` portlarını orkestre eder. Atomic transaction sink adapter'ının sorumluluğundadır.

## Kanıt

- Source batch tenant/company/party-account/effective-as-of/recorded-cutoff alanları istekle birebir eşleşmeden builder çalışmaz.
- Policy bulunamazsa veya subledger/GL kanıt çifti yoksa publish yapılmaz.
- Statement ve aging tek report definition/generation kesiminde üretilip exact cross-foot edilir.
- İki balance snapshot'ı source control account ve ortak report slice ile eşleşmeden sink çağrılmaz.
- Geçerli fixture sink'e tam bir kez ulaştı.
- Farklı company isteyen query'ye dönen source batch `PARTY_REPORT_SOURCE_SCOPE_MISMATCH` ile zero-publish reddedildi.
- Başka control-account kimliği taşıyan balance evidence `PARTY_CONTROL_ACCOUNT_EVIDENCE_MISMATCH` ile zero-publish reddedildi.
- Release solution build 0 warning/0 error; architecture/application contract hostu 19 source project için geçti.

## Açık işler

Gerçek Parties adapter'ı balance-side ve opening kanıtı şemada olmadığı için blokeli kalır. Aging policy ve Accounting/Parties control-balance port adapter'ları, PostgreSQL atomic sink adapter'ı ve Worker scheduling sonraki teknik dilimlerdir. Production permission code ve kullanıcı sahipleri `atanmadı` olarak kalır.
