# TASK-XXXX — Sonuç odaklı başlık

> Master fazı/kapısı: MP-XX / ...  
> Risk sınıfı: R0 / R1 / R2 / R3 / R4  
> Durum: proposed / ready / in-progress / blocked / validating / completed / superseded  
> Definition of Ready: passed / conditional / blocked

## Amaç

Kullanıcının elde edeceği doğrulanabilir sonuç.

## Bağlam

- İlgili requirement/issue:
- İlerletilen master kapısı:
- Okunacak zorunlu docs/ADR/kod:
- Koşullu belgeler ve tetikleyicileri:
- Mevcut davranış:
- Birincil araştırma/standart:
- Karşılaştırılan açık kaynak davranışı:
- Kabul/red ve clean-room gerekçesi:

## Kapsam

- Dahil:
- Hariç:
- Varsayımlar/açık sorular:

## Değişmezler ve riskler

Finansal, tenant/scope, security/privacy, concurrency/idempotency, mevzuat ve migration.

## Plan

- [ ] Keşif ve tasarım.
- [ ] Domain/DB/API.
- [ ] Web/Android gerekiyorsa.
- [ ] Audit/outbox/telemetry.
- [ ] Test/migration/docs.

Plan ekleri:

- BPMN/exception/telafi.
- REA ve kaynak gerçek.
- Accounting impact + reversal.
- Effective/recorded/posted date ve lock/cut-off.
- Control owner/evidence.
- Report/reconciliation.

## Tamamlanmış sayılma

- [ ] Ölçülebilir kabul senaryoları.
- [ ] Çalıştırılacak test/komutlar ve beklenen sonuç.
- [ ] Rollback/compensation.
- [ ] OpenAPI/ADR/docs güncel.
- [ ] Kaynak→alt defter→GL ve varsa allocation/bank reconciliation.
- [ ] Golden scenario, control-account ve report cross-foot.
- [ ] Business correction ile projection repost ayrımı.
- [ ] Süreç sahibi/kullanıcı kabulü veya kayıtlı no-go.
- [ ] Master fazına etkisi değerlendirildi; kapı ilerlediyse kanıtı MASTER_PLAN.md dosyasına işlendi.

## Teslim notu

Değişen sonuç, test kanıtı, migration/deploy notu, açık risk, planın güncel durumu ve sıradaki tek kesin adım.

Ek olarak source/rule/profile version, as-of/generation, mutabakat farkı ve çalıştırılan kontrol kanıtını belirt.
