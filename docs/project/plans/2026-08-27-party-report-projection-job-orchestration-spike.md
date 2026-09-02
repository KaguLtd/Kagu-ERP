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
- 29 Ağustos güncellemesinde control-account portu exact `PartyReportSourceBatch` bağlamını zorunlu aldı; source lineage GL kanıtına kadar taşındı.
- Aynı scope/slice içinde sıfır olmayan subledger−GL farkı `PARTY_CONTROL_ACCOUNT_RECONCILIATION_DIFFERENCE` ile zero-publish reddedildi.
- Gerçek PostgreSQL `75 GBP` due kaynağı job ve atomic sink üzerinden persisted statement/aging/control-account setine taşındı; tam command replay'i `Created=false` kaldı.
- Release solution build 0 warning/0 error; architecture/application contract hostu 19 source project için geçti.

## Açık işler

Gerçek Parties adapter'ı, PostgreSQL atomic sink, Accounting exact-lineage control-balance bileşimi, effective/recorded kesimli authoritative aging-policy seçimi ve production permission/API tamamlandı. Worker scheduling için hangi service identity'nin hangi company scope ile çalışacağı ve iptal edilmiş kullanıcı yetkisinden bağımsız otorite sınırı henüz belirlenmedi; kullanıcı hesabı sessizce taklit edilmedi. Opening event'in due-date/open-item semantiği de açık kalır. Kullanıcı sahipleri `atanmadı` olarak kalır.
