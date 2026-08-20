# Yedekleme, Geri Yükleme ve Felaket Kurtarma

## 1. Hedefler

Başlangıç hedefi:

- **RPO ≤ 15 dakika:** felakette kabul edilen azami veri kaybı.
- **RTO ≤ 4 saat:** temel satış/stok/cari/finans hizmetinin geri dönüşü.

E-fatura/ödeme gibi dış sisteme gönderilmiş işlemler RPO hesabından ayrı olarak dış referanslarla yeniden mutabık edilir. İşletmenin hedefi daha sıkıysa topology, bütçe ve vardiya/on-call yeniden tasarlanır.

## 2. Korunacak veri envanteri

| Veri | Yöntem | Öncelik |
|---|---|---|
| ERP PostgreSQL | pgBackRest full/differential + continuous WAL | kritik |
| Keycloak PostgreSQL | pgBackRest/uygun tutarlı DB yedeği | kritik |
| Belge/e-fatura blobları | restic snapshot + immutable uzak kopya | kritik |
| Caddy data/config | restic | yüksek |
| Uygulama config/manifest | restic + Git/artefakt deposu | yüksek |
| Secrets/şifreleme anahtarları | ayrı şifreli escrow/secret store export | kritik |
| Monitoring/audit dış kopyası | retention politikasına göre | yüksek |
| Container image/SBOM | registry + digest manifest | yüksek |

Container writable layer yedek değildir. Veritabanı dosya dizinini çalışan sistemde sıradan dosya kopyasıyla almak tutarlı DB yedeği sayılmaz.

## 3. 3-2-1-1-0 politikası

- En az 3 veri kopyası.
- En az 2 farklı ortam/medya.
- En az 1 kopya farklı fiziksel lokasyonda.
- En az 1 kopya çevrim dışı veya immutable/object-lock.
- 0 doğrulanmamış hata: otomatik check + düzenli gerçek restore.

Uzak kopya KKTC dışındaki bir bulut/ülkedeyse kişisel veri transfer ruhsatı ve sözleşme kararı tamamlanmadan kullanılmaz.

## 4. PostgreSQL politikası

Önerilen başlangıç:

- haftalık full,
- günlük differential,
- sürekli WAL arşivleme,
- en az 35 günlük PITR penceresi,
- aylık/yıl sonu kopyaları hukuki/mali saklama kararına göre daha uzun,
- repository cipher ve ayrı erişim kimliği,
- günlük `check`, backup age ve WAL archive lag alarmı.

Takvim iş yükü ve depo kapasitesiyle doğrulanır. Retention yalnız takvim değil; açık denetim/yasal hold ve dönem sonu ihtiyacını dikkate alır.

## 5. Blob/config politikası

- Restic snapshot günlük ve kritik e-fatura arşivinde daha sık tetiklenebilir.
- İçerik hash/manifest DB ile birlikte saklanır.
- DB ve blob kesimlerinin tutarlı ilişkisi için backup epoch/manifest oluşturulur.
- Restore sırasında eksik blob/orphan DB referansı raporlanır.
- E-fatura arşivinin periyodik okunabilirlik ve hash doğrulaması yapılır.
- Config yedeğine secret yalnız ayrı şifreli yöntemle; düz `.env` arşivlenmez.

## 6. Yedek güvenliği

- Üretim runtime hesabı backup repository silemez.
- Backup hesabı ayrı, en az yetkili ve MFA/rotasyonlu.
- Repository anahtarı verinin yanında tek kopya değildir; çift kontrollü escrow.
- Backup logunda secret/PII yok; erişim audit edilir.
- Immutable retention yöneticinin günlük hesabıyla kısaltılamaz.
- Fidye yazılımı senaryosunda üretim kimliğinin uzak kopyayı silemediği test edilir.

## 7. İzleme

Kritik alarm:

- son başarılı full/diff yaşı,
- son arşivlenen WAL zamanı ve arşiv kuyruğu,
- repository kapasitesi/immutability,
- backup/check hata veya beklenmeyen süre,
- snapshot sayısı/retention sapması,
- şifreleme anahtarı/credential süresi,
- son başarılı restore tatbikatının yaşı.

“Job exit 0” tek başına başarı değildir; beklenen dosya/WAL aralığı ve restore edilebilirlik ölçülür.

## 8. Restore kararları

| Olay | Yöntem |
|---|---|
| Tek kullanıcı hatası | iş ters/düzeltme kaydı; DB restore yok |
| Tek silinmiş/bozuk belge blobu | restic'ten ayrı alana restore, hash ve DB link kontrolü |
| Hatalı deploy/migration | roll-forward/uyumlu rollback; gerekirse izole PITR ve veri çıkarımı |
| DB corruption/host kaybı | yeni/temiz hosta pgBackRest restore + PITR |
| Ransomware | ağdan izole temiz host, immutable kopya, credential rotasyonu |
| Dış sistem sonuç belirsizliği | restore sonrası banka/e-fatura resmi durum mutabakatı |

PITR tüm veritabanını zamana döndürür; tek kayıt düzeltmek için varsayılan araç değildir.

## 9. Tam felaket geri yükleme runbook'u

1. Incident komutanı olayı sınıflandırır; etkilenen sistemi izole eder.
2. Yeni yazmaları durdurur, saat/korelasyon ve dış entegrasyon durumunu korur.
3. Kurtarma noktası; son iyi backup, WAL ve iş kayıtlarıyla seçilir.
4. Temiz, hardening'i tamamlanmış yeni host hazırlanır.
5. Image digest/config manifest ve secrets güvenli kaynaktan geri getirilir.
6. PostgreSQL/Keycloak DB restore ve hedef PITR yapılır.
7. Blob/config/Caddy verisi geçici alana restore edilir; hash/izin doğrulanır.
8. Ağ kapalıyken schema, constraint, DB check ve golden mali kontroller çalışır.
9. Uygulama read-only başlatılır; şirket/rol/auth/audit doğrulanır.
10. Outbox/inbox ve dış sistemdeki ödeme/e-fatura referansları mutabık edilir.
11. Mali kontroller: mizan borç=alacak, cari, stok, banka, çek, KDV ve belge hash.
12. İş sahibi + teknik + güvenlik onayıyla dış erişim açılır.
13. Biriken işlerin kontrollü işlenmesi ve yoğun izleme.
14. RPO/RTO, kayıp/tekrar/uyumsuzluk ve hukuki bildirim raporu.

## 10. E-fatura veri kaybı/bozulması

KKTC e-fatura kuralındaki üç iş günlük bildirim ihtimali için özel prosedür:

- Olay tespit zamanı ve iş günü sayacı sistemde kaydedilir.
- Etkilenen belge aralığı, numaralar, gönderim/resmi durum ve hash listelenir.
- Yedek/arşiv ve resmi sistem kopyaları karşılaştırılır.
- İşlemlerin nasıl tamamlanacağı veya verinin nasıl geri getirileceği planlanır.
- Gelir ve Vergi Dairesi bildirimi yalnız yetkili şirket temsilcisi/hukuk-mali müşavir onayıyla yapılır.
- Gönderim kanıtı incident paketine eklenir.

Bu süre sistem alarmı ve runbook SLA'sıdır; otomatik dış bildirim değildir.

## 11. Tatbikat takvimi

MP-02 local smoke, `scripts/test-restore.ps1`/`.sh` ile kaynak Compose DB'sinden custom-format dump alıp yalnız rastgele ve doğrulanmış `kagu_erp_restore_*` adlı ayrı DB'ye restore eder. Restore hedefinde migration, DB role/RLS/IAM/audit/outbox ve desteklenen ortamda Keycloak auth scope testleri çalışır; kaynak DB/volume değiştirilmez. Geçici DB ve dump doğrulanmış hedef adıyla her durumda temizlenir. Bu küçük sentetik smoke aşağıdaki production hacim/PITR/blob/Keycloak tatbikatlarının yerine geçmez.

- Aylık: rastgele DB backup'ını izole ortamda açma ve otomatik mali smoke.
- Üç aylık: tam DB + blob + Keycloak restore; RPO/RTO ölçümü.
- Altı aylık: host kaybı/ransomware tabletop ve temiz host kurulumu.
- Yıllık: iş birimleri, mali müşavir, güvenlik ve dış entegrasyonlarla tam DR.
- Her major upgrade/migration sonrası hedefli restore.

Her tatbikat üretime benzer hacimde olmalı; küçük boş DB restore'u yeterli kanıt değildir.

## 12. Restore kabul kriterleri

- Seçilen noktaya kadar transaction ve WAL zinciri eksiksiz.
- Tüm DB constraint/migration sürümü doğru.
- Borç=alacak; alt defter mutabakatları açıklanan toleransta.
- Blob/e-fatura hash ve görüntülenebilirlik örneklemi başarılı.
- Login/MFA/rol/şirket izolasyonu çalışıyor.
- Outbox yeniden başlaması çift ödeme/e-fatura üretmiyor.
- RPO/RTO hedefleri ölçülüp kayıtlı.
- Restore ortamındaki hassas veri erişimi ve sonradan güvenli imha kayıtlı.

## 13. İş sürekliliği

Tam sistem yokken onaylı manuel prosedürler: satış/teslim geçici numara kayıtları, stok hareket formu, tahsilat makbuzu kontrolü ve sonradan çift kontrollü içe aktarma. Resmi e-fatura veya banka işlemi için mevzuat/sağlayıcı prosedürü esas alınır; offline sistem kendi resmi numarasını uydurmaz.

## 14. Kanonik ve türetilmiş veri sınıfları

Restore önceliği:

1. PostgreSQL canonical business events, subledgers, allocations, GL ve audit;
2. legal archive/attachments ve raw integration objects;
3. Keycloak/config/secrets/certificates;
4. outbox/inbox ve job checkpoint;
5. yeniden üretilebilir reporting projections/cache.

Projection yedeği hızlı açılış sağlayabilir ancak canonical backup’ın yerine geçmez. Projection restore edilirse source watermark/generation uyumu doğrulanır; uyumsuzsa discard + rebuild yapılır.

## 15. Finansal restore doğrulama paketi

Teknik health yeterli değildir. Her restore provasında:

- journal debit=credit ve orphan source/reversal;
- AR/AP open-item + allocation toplamları ve GL control account;
- stock ledger quantity/value ve inventory GL;
- bank statement opening/closing/control totals ve bank GL;
- cheque portfolio/custody ve GL;
- sequence/gap events;
- outbox/inbox duplicate ve unprocessed age;
- trial balance, balance sheet/P&L ve seçilmiş golden report checksum;
- archive object count/hash ve evidence package

doğrulanır. Sonuçlar restore timestamp, target PITR, backup IDs, commit/release, row/control totals, fark ve sign-off ile saklanır.

## 16. PITR ve dış dünya sınırı

PITR hedefi dış sağlayıcının kabul ettiği e-Fatura/banka mesajlarından önceye dönebilir. Restore sonrası “DB’de yok” dış işlemi yok etmez. Reconciliation recovery:

1. provider accepted/status log ve raw response envanteri;
2. restored outbox/inbox ile external ID/hash karşılaştırması;
3. eksik local sonucu idempotent status query/import ile tamamlama;
4. duplicate resend’i engelleme;
5. insan onaylı exception listesi

uygular. Yeni resmi numara veya payment üretmeden önce dış gerçek doğrulanır.

## 17. Manuel süreklilikten geri giriş

Kesinti formları unique temporary reference, actor, captured time, company/scope ve kanıt taşır. Sistem geri geldiğinde iki kişi:

- duplicate/search;
- gerçek effective date ve açık dönem/correction policy;
- belge numarası/resmi portal sonucu;
- stok/cari/payment/allocation/GL etkisi

kontrolüyle import eder. Manuel kayıtlar batch/reconciliation raporuyla kapanmadan normal kapanışa geçilmez.
