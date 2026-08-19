# İş Akışı ve Onay Modülü

## 1. Amaç

Satın alma, ödeme, iskonto, iade, manuel fiş, dönem açma, banka hesabı değişikliği ve benzeri riskli işlemler için ortak, sürümlü ve denetlenebilir onay altyapısı sağlar.

## 2. Ana model

- `workflow_definition`, `workflow_version`
- `workflow_step`, `workflow_condition`
- `approval_instance`, `approval_task`
- `approval_decision`, `delegation`
- `approval_policy`, `authority_limit`
- `sla_timer`, `escalation_event`
- `separation_of_duties_rule`

İş akışı sürümü, işlem gönderildiği anda sabitlenir. Sonraki politika değişikliği devam eden akışı sessizce değiştirmez; yetkili göç kararı gerekir.

## 3. Kural girişleri

- şirket/şube,
- belge/işlem türü,
- tutar ve para birimi,
- yerel para karşılığı ve kullanılan kur,
- masraf merkezi/proje,
- talep eden ve organizasyon hiyerarşisi,
- tedarikçi/müşteri risk sınıfı,
- istisna, iskonto veya banka bilgisi değişikliği,
- görevler ayrılığı çatışması.

Kur, gönderim anında anlık görüntülenir; limit değerlendirmesi sonradan kur değişince geriye dönük değişmez.

## 4. Adım tipleri

- tek onaycı,
- aday gruptan herhangi biri,
- tüm onaycılar,
- sıralı onay,
- paralel onay,
- tutar kademeli onay,
- otomatik kontrol,
- bilgi/onay gerektirmeyen bildirim.

İşlem: `approve`, `reject`, `request_change`, `cancel`, `delegate`, `escalate`. Her karar gerekçe, kullanıcı, zaman, istemci, kapsam ve hedef belge sürümünü taşır.

## 5. Değişmez kurallar

- `WFL-INV-001`: Onay yalnız görev atanmış ve güncel kapsamı olan kullanıcı tarafından verilir.
- `WFL-INV-002`: Onaylanan belge sürümü değişirse önceki onay geçersiz olur veya yeniden değerlendirme politikası çalışır.
- `WFL-INV-003`: Görevler ayrılığı kuralı, sistem yöneticisi dahil sessizce aşılamaz.
- `WFL-INV-004`: Yetki limitleri para birimi dönüşümüyle deterministik uygulanır.
- `WFL-INV-005`: Karar geçmişi silinemez; iptal/yeni akış bağlantılı tutulur.
- `WFL-INV-006`: Delegasyon başlangıç/bitiş tarihli, kapsamlı ve denetlenebilirdir; devreden kişi kendi görevini devralamaz.
- `WFL-INV-007`: Aynı görevin paralel karar yarışında yalnız ilk geçerli sonuç kabul edilir.

## 6. SLA ve vekâlet

SLA iş takvimine ve KKTC yerel saatine göre hesaplanır. Hatırlatma/escalation, karar yerine geçmez. İzin vekâleti önceden zamanlanabilir; kritik ödeme veya dönem açma yetkisi için ayrıca onay gerekebilir. Süresi biten vekâlet otomatik kapanır.

## 7. API ve istemciler

- `GET /api/v1/approval-tasks?assignee=me`
- `POST /api/v1/approval-tasks/{id}/decisions`
- `POST /api/v1/workflow-definitions/{id}/publish`
- `POST /api/v1/delegations`
- `GET /api/v1/workflows/{id}/timeline`

Web: toplu seçim yalnız aynı karar/kural bağlamındaki düşük riskli görevlerde. Android: görev listesi, güvenli belge özeti ve tekil karar; yüksek riskte yeniden kimlik doğrulama. Her istemci sunucu kararını esas alır.

## 8. Güvenlik ve testler

- Yatay/dikey yetki yükseltme denemeleri.
- Kendi talebini onaylama ve dolaylı vekâlet döngüleri.
- Belge değişirken eşzamanlı onay yarışı.
- Limit sınırı, negatif tutar, farklı kur ve yuvarlama.
- Paralel/tüm onaycı akışları ve reddetme davranışı.
- SLA, hafta sonu/tatil ve saat dilimi.
- Bildirim tekrarının karar tekrarına dönüşmemesi.
- İş akışı sürümünün geçmiş örnekleri aynı sonucu vermesi.

## 9. Quorum, farklı kişi ve kontrol kanıtı

ApprovalStep yalnız approver count değil distinct-person quorum taşır. Aynı kullanıcı, vekâlet, paylaşılan hesap veya aynı service identity birden çok gerekli oyu dolduramaz. Maker, kaynak kaydı son değiştiren, lehtar banka hesabını değiştiren ve repost/reopen isteyen kişiler policy’ye göre approver olamaz.

Delegation:

- başlangıç/bitiş, kapsam, reason ve delegator/delegatee taşır;
- daha yüksek permission veya scope üretmez;
- SoD çatışmasını çözmez;
- kritik işlemde delegator ve delegatee’yi ayrı kişi sayarak quorum doldurmaz;
- süre sonu otomatik kapanır ve açık görevler yeniden atanır.

Kararı etkileyen belge tutarı, currency conversion, risk, iskonto, banka hesabı, rule version ve attachment hash’i approval snapshot’ına girer. Bu girdiler değişirse onay otomatik geçerli kalmaz; policy’nin belirlediği aşamadan reset olur.

## 10. Exception, escalation ve BPMN davranışı

Escalation bildirim veya yeniden atamadır; otomatik approval değildir. Timeout, approver unavailable, rejected, withdrawn, superseded ve policy-error ayrı state’lerdir. Her state için izinli komut, owner, SLA ve audit kanıtı vardır.

Parallel gateway’de tüm/çoğunluk/any-one semantiği açık policy’dir. Rejection’ın tüm süreci mi, yalnız adımı mı döndürdüğü; değişiklik sonrası hangi approvals’ın geçersiz olduğu sürümlenir. Compensation business reversal değildir; önce iş modülünün güvenli command’ını çağırır.

Kontrol raporu approval instance, rule snapshot, eligible approver set, karar sırası, delegation, SLA exception ve aynı kişinin kritik görevlerdeki rol çakışmasını gösterir.

Ek testler quorum bypass, role değişirken açık görev, tutar/IBAN değişiminde reset, delegation expiry, paralel yarış ve retry idempotency’yi kapsar.
