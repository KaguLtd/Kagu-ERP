# Belge, Denetim İzi ve Bildirim Modülü

## 1. Amaç

Dosya eklerini güvenli saklamak, kritik işlemlerin kim-ne-zaman-ne yaptı izini korumak ve kullanıcı bildirimlerini iş işleminden güvenilir biçimde ayırmak için ortak platform hizmetleri sağlar.

## 2. Belge yönetimi

Ana varlıklar: `document`, `document_version`, `attachment_link`, `retention_policy`, `legal_hold`, `malware_scan`, `document_access_event`.

Yükleme akışı:

1. Ön imzalı veya kontrollü yükleme oturumu oluşturulur.
2. Uzantı değil gerçek MIME/imza, boyut ve dosya adı doğrulanır.
3. Dosya karantina alanına alınır; hash hesaplanır ve zararlı yazılım taranır.
4. Temiz dosya şifreli asıl depoya taşınır.
5. İş belgesi bağlantısı, sınıflandırma ve saklama politikası kaydedilir.
6. İndirme her seferinde yetki kontrolü ve audit olayı üretir.

İzin verilen dosya tipleri beyaz listedir. SVG/HTML gibi aktif içerik varsayılan olarak reddedilir veya güvenli dönüştürülür. Kullanıcı dosya yolu belirleyemez.

## 3. Saklama ve bütünlük

- İçerik adreslemeli veya rastgele nesne anahtarı; kullanıcı dosya adı sadece metadata.
- İçerik hash'i ve boyut, yedek/restore sonrası doğrulanır.
- Yasal saklamadaki dosya kullanıcı veya normal bakım süreciyle silinemez.
- Saklama süreleri belge türü ve yargı alanına göre sürümlenir.
- Fiziksel silme ayrı, çift onaylı ve raporlu yaşam döngüsü işidir.
- E-fatura arşivi daha güçlü değişmezlik ve resmi görüntülenebilirlik kurallarına tabidir.

## 4. Denetim izi

`audit_event` aşağıdakileri taşır:

- olay kimliği, zaman ve korelasyon/trace kimliği,
- aktör, oturum, istemci ve doğrulama seviyesi,
- şirket/şube kapsamı,
- eylem ve hedef tür/kimlik,
- başarı/ret ve politika nedeni,
- önce/sonra değerlerinin izinli ve maskelenmiş özeti,
- kaynak IP/cihaz bilgisi için veri minimizasyonu,
- önceki olay hash'i veya dönemsel imza zinciri.

Parola, access/refresh token, secret, tam banka kartı veya gereksiz kişisel veri audit'e yazılmaz. Audit kaydı uygulama yöneticisi tarafından değiştirilemez; ayrı yazma rolü ve salt ekleme yetkisi kullanılır.

## 5. Bildirim modeli

İşlem, aynı veritabanı transaction'ında `notification_intent`/outbox kaydı üretir. Worker; uygulama içi, e-posta, push veya ileride SMS kanalına teslim eder.

- İş olayının başarısı e-posta sağlayıcısına bağlı değildir.
- Tekilleştirme anahtarı yinelenen bildirimi önler.
- Şablonlar sürümlü, yerelleştirilebilir ve XSS güvenlidir.
- Hassas belge ayrıntısı e-posta/push içinde verilmez; güvenli uygulama bağlantısı kullanılır.
- Kullanıcı tercihleri zorunlu güvenlik/uyum bildirimlerini kapatamaz.
- Teslim sonucu, yeniden deneme ve dead-letter iş kuyruğu izlenir.

## 6. API ve ekranlar

- `POST /api/v1/documents/uploads`
- `GET /api/v1/documents/{id}/download`
- `GET /api/v1/audit-events?targetType=...&targetId=...`
- `GET /api/v1/notifications`
- `POST /api/v1/notifications/{id}/read`
- `POST /api/v1/retention-holds`

Ekranlar: belge paneli/sürüm geçmişi, audit zaman çizelgesi, bildirim merkezi, karantina yönetimi, saklama/hold yönetimi.

## 7. Değişmez kurallar

- `DOC-INV-001`: Tarama tamamlanmadan dosya iş kullanıcısına sunulmaz.
- `DOC-INV-002`: Dosya hash'i değişirse yeni sürümdür; mevcut sürüm yerinde değişmez.
- `AUD-INV-001`: Finansal/kimlik/yetki olayları audit olmadan sonuçlanamaz.
- `AUD-INV-002`: Audit sorgusu şirket ve hassasiyet yetkisini uygular.
- `NOT-INV-001`: Bildirim tekrar denemesi iş komutunu tekrar çalıştırmaz.
- `NOT-INV-002`: Bildirimdeki bağlantı tek başına yetki sağlamaz.

## 8. Testler

- Çift uzantı, MIME uyuşmazlığı, zararlı/arşiv bombası, yol geçişi.
- Yetkisiz belge kimliği tahmini ve süresi dolmuş indirme bağlantısı.
- Hash/versiyon/yasal hold ve restore sonrası bütünlük.
- Audit atlatma, maskeleme ve zincir doğrulaması.
- Outbox yeniden oynatma, sağlayıcı kesintisi, yinelenen webhook.
- E-posta/push içeriğinde PII ve token sızıntısı kontrolleri.

## 9. Kanıt paketi ve zaman semantiği

AuditEvent occurred/effective time ile recorded_at/system time’ı karıştırmaz. Actor, impersonation/delegation, tenant/company/scope, command, target, before/after field policy, reason, correlation, source IP/device risk ve outcome taşır. Finansal kaynak olayın kendi değişmez kaydı audit log’un yerine geçmez; ikisi birbirini referanslar.

EvidencePackage aşağıdaki manifesti tek kimlikle toplar:

- kaynak business document ve line referansları;
- ek dosya hash’leri ve legal archive object version;
- approval/policy snapshot;
- tax/posting/rate/rounding snapshot;
- alt defter, allocation, GL ve report generation kimlikleri;
- dış zarf/webhook/banka statement raw hash’i;
- reversal/correction/repost zinciri;
- export filter, as-of, timezone ve checksum.

Paket yeniden üretilebilir metadata taşır fakat geçmiş resmi belge bytes’ını yeni şablonla üretip “aynı” saymaz.

## 10. Audit raporları ve anomali kontrolleri

- posted/reversed/reposted hareket ve source lineage;
- manual journal, period reopen ve rule/master-data değişikliği;
- banka/party IBAN değişikliği ile yakın zamanlı ödeme;
- aynı kişinin maker/approver/reconciler rol çakışması;
- sequence gap/void, attachment replacement ve failed access;
- yüksek hacimli export, break-glass ve privilege grant;
- control execution/missed control ve unresolved exception

raporları denetçi rolüne scope/maskeleme ile sunulur. Operasyon log retention’ı ile yasal audit retention’ı ayrı; log silinmesi kaynak mali kanıtı silemez.

Bildirim, kontrolün yürüdüğünün kanıtı değildir. Kritik onay/exception için notification delivery ve insan karar kaydı ayrı ölçülür; e-posta içeriği yalnız güvenli bağlantı ve minimum bağlam içerir.
