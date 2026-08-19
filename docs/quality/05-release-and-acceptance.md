# Sürüm, Dağıtım ve Kabul Planı

## 1. Ortamlar

- **local:** geliştirici, sentetik veri, fake sağlayıcı.
- **CI:** kısa ömürlü, her build izole.
- **staging:** üretime yakın topology; resmi sandbox; sentetik/anonim veri.
- **production:** müşteri verisi; değişiklik yalnız onaylı pipeline.
- **restore-lab:** yedek tatbikatı ve adli inceleme; prod ağına kapalı.

Ortamlar secret, hostname, identity realm/client, veritabanı, blob ve dış sistem hesapları bakımından ayrıdır. Üretim dışı sistem üretim ödeme/e-fatura gönderemez.

## 2. Sürüm artefaktı

Her release:

- semantik sürüm ve Git commit/tag,
- değişmez container image digest'leri,
- veritabanı migration kimliği,
- OpenAPI ve olay sözleşmesi sürümü,
- web build ve Android uyumluluk aralığı,
- SBOM, imza/provenance ve tarama raporu,
- release notes ve bilinen riskler,
- config schema ve feature flag durumu,
- rollback/roll-forward talimatı

taşır. `latest` etiketi dağıtım kanıtı değildir.

## 3. CI/CD akışı

1. lint/format/compile,
2. unit/property ve integration test,
3. OpenAPI/DB/olay backward compatibility,
4. SAST/SCA/secret/IaC/image tarama,
5. image build, SBOM ve digest,
6. staging deploy + migration,
7. smoke/E2E/performance güven testi,
8. release adayı ve onay,
9. üretim backup/preflight,
10. controlled deploy + migration + smoke,
11. gözlem penceresi ve release kararı.

CI doğrudan SSH ile rastgele komut çalıştırmaz; sürümlü dağıtım scripti/runbook'u kullanır. Üretim onayı rol tabanlı ve auditlidir.

## 4. Veritabanı migration stratejisi

Expand–migrate–contract:

- önce geriye uyumlu tablo/kolon ekle,
- uygulamayı çift-okuma/yazma gerekiyorsa kontrollü geçir,
- veriyi küçük ve gözlemlenebilir batch'lerle backfill et,
- doğrula ve yeni davranışı etkinleştir,
- eski alanı en az bir destek penceresi sonra kaldır.

Uzun tablo kilidi veya veri dönüşümü deploy request'inde çalıştırılmaz. Migration staging'de üretim benzeri hacimle süre/lock testinden geçer. Finansal veriyi geri döndürülemez değiştiren migration için onaylı yedek ve roll-forward scripti zorunludur.

## 5. Dağıtım yaklaşımı

Tek host Compose'ta:

- yeni image'lar önceden çekilir ve digest doğrulanır,
- health/readiness check geçmeyen servis trafiğe alınmaz,
- Caddy sabit edge olarak kalır,
- API sürümü web ve desteklenen Android sürümleriyle uyumlu başlatılır,
- workerlar migration sonrası kontrollü açılır,
- feature flag riskli entegrasyonu sonradan etkinleştirir.

Gerçek zero-downtime garanti edilmez; kısa bakım penceresi dürüstçe planlanır. Tek host arızası için restore/failover planı uygulanır.

## 6. Rollback / roll-forward

- Statik web: önceki digest'e dönüş.
- API: DB şeması geriye uyumluysa önceki digest; değilse tercih roll-forward düzeltmedir.
- Migration: yalnız veri kaybetmeyen, testli down migration; aksi halde PITR yalnız açık felaket kararıyla.
- Mobil: dağıtılmış APK geri çağrılamaz; API en az tanımlı N/N-1 sürümlerini destekler ve minimum sürüm politikası vardır.
- Finansal yanlışlık: kaydı DB'den silmek yerine iş ters/düzeltme akışı ve kod düzeltmesi.

Geri dönüş karar ölçütü: hata oranı/SLO, mali invariant, migration/queue, auth, veri sızıntısı veya entegrasyon çift işlem riski.

## 7. Fonksiyonel kabul

Her epik/modül için:

- kapsam ve gereksinim kimlikleri,
- olumlu/olumsuz/rol/yetki senaryoları,
- mali/stok/cari sonuç ve mutabakat,
- audit/notification/outbox sonucu,
- web/mobil uygun durumlar,
- rapor ve dışa aktarma,
- kullanıcı sahibi imzası

bulunur. Demo kabul değildir; test ortamında tekrar edilebilir kanıt gerekir.

## 8. Pilot kabul

- Sınırlı şirket/şube ve gerçek kullanıcı grubu.
- Onaylı başlangıç verisi ve günlük kaynak–hedef kontrolü.
- Mali müşavir günlük/haftalık mizan/cari/stok mutabakatı.
- Destek hattı, incident severity ve cevap sahibi.
- Kullanıcı eğitimi ve kısa görev kılavuzları.
- En az bir dönem sonu prova veya simülasyonu.
- Kritik işlerde geri dönüş/manual continuity prosedürü.

## 9. Go-live kontrol listesi

- [ ] Hukuki/KKTC açık soruları ve gerekli resmi onaylar kapalı.
- [ ] Üretim hostname/TLS/DNS ve firewall doğrulandı.
- [ ] Keycloak MFA, admin ve break-glass kontrol edildi.
- [ ] Son restore tatbikatı RPO/RTO içinde.
- [ ] Golden mali veri ve migration mutabakatı geçti.
- [ ] Kritik E2E/güvenlik/performans testleri yeşil.
- [ ] Monitoring/alert/on-call/runbook aktif.
- [ ] Dış entegrasyon test/üretim ayrımı doğrulandı.
- [ ] Rollback ve iş sürekliliği sorumluları hazır.
- [ ] Kullanıcı/rol/şirket erişim listesi onaylandı.
- [ ] Release artefaktı, digest ve config yedeği saklandı.

## 10. Üretim sonrası doğrulama

İlk 15 dakika ve gözlem penceresinde: health, auth, yetki, basit okuma/yazma, outbox, e-posta test hedefi, DB lock/error, log/trace, disk ve backup scheduler. Finansal smoke sentetik veya kontrollü terslenebilir işlemle; gerçek müşteri ödemesi/e-fatura yalnız iş onayıyla yapılır.

24/72 saat sonra hata, latency, kuyruk, mutabakat, kullanıcı geri bildirimi ve kapasite gözden geçirilir; release kaydı kapatılır veya düzeltme planı açılır.

## 11. Muhasebe çekirdeği release kapıları

- Golden process cycles ve property/invariant testleri yeşil.
- Kaynak→alt defter→GL→rapor drill-down ve control-account sıfır fark.
- Allocation, bank reconciliation, backdate/repost ve ayrı lock scope testleri.
- Posting/reconciliation exception queue owner ve runbook’ları hazır.
- Rapor/export as-of, generation ve control totals doğrulanmış.
- Restore sonrası ledger rebuild ve aynı mali rapor checksum/mutabakat kanıtı.
- Mevzuat/rule/profile snapshot’larının source URL/hash/onayı kayıtlı.

Bu kapılardan biri başarısızsa UI’nin çalışması veya migration’ın teknik tamamlanması release için yeterli değildir.

## 12. Organizasyonel kabul ve hypercare

ERP başarısı deployment değildir. Go-live öncesi:

- süreç ve kontrol sahipleri;
- role göre eğitim ve yetkinlik kontrolü;
- super-user/destek rotası;
- eski/yeni süreç farkı ve yasak workaround’lar;
- iş sürekliliği/manual capture ve sonradan kontrollü giriş;
- ilk ay/çeyrek kapanış takvimi

onaylanır.

30/60/90 gün gözden geçirmesi; aktif/doğru kullanım, manual workaround, exception yaşı, duplicate/reversal, kapanış süresi, reconciliation farkı, support ticket, kullanıcı hata/güven ve hedeflenen iş faydasını ölçer. Hedef sapması ürün backlog’una owner/date ile girer; teknik olarak “stabil” olduğu için kapatılmaz.

## 13. Paralel çalışma ve go/no-go

En az iki ardışık dönem veya risk sahibince onaylanan eşdeğer prova boyunca kaynak sistem/uzman çalışma dosyası ile:

- belge sayı/tutar;
- AR/AP aging;
- stock quantity/value;
- bank/cash/cheque;
- tax workpaper;
- trial balance ve mali tablolar

karşılaştırılır. Material fark, owner ve onaylı disposition olmadan no-go’dur. Cutover sonrası legacy write kapatma ve new system sequence start çift kontrol edilir.
