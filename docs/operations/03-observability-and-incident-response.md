# Gözlemlenebilirlik ve Olay Müdahalesi

## 1. Hedef

Sistemin ne yaptığını, kullanıcı etkisini ve finansal/entegrasyon durumunu tek korelasyon zinciriyle anlayabilmek. OpenTelemetry tabanlı metric, structured log ve trace üretimi; başlangıçta Prometheus/Grafana/Loki/Tempo veya kuruma uygun eşdeğer arka uç.

## 2. Telemetri standardı

Her HTTP/worker/DB/dış çağrı:

- `trace_id`, `span_id`, `correlation_id`,
- servis/sürüm/ortam,
- route veya iş türü (ham URL/ID değil),
- durum, latency, hata sınıfı,
- izinli takma `company_id`,
- outbox/job id,
- dış sağlayıcı ve masked referans

taşır. Müşteri adı, VKN, fatura satırı, IBAN, token, cookie, secret, dosya içeriği ve SQL parametresi loglanmaz.

## 3. Sinyaller

### API/web/mobile

- RED: request rate, error, duration.
- Endpoint p50/p95/p99, 4xx/5xx ve timeout.
- Auth başarı/ret/MFA ve authorization deny sayıları.
- Web vitals; Android crash/ANR/start/sync.

### PostgreSQL

- bağlantı/pool, lock/wait/deadlock,
- query latency ve top normalized query,
- transaction/rollback,
- cache hit, temp file, WAL/checkpoint,
- autovacuum/analyze lag ve tablo/index büyümesi,
- archive/replication/backup durumu.

### İş metrikleri

- postalanamayan muhasebe olayları,
- mizan/alt defter fark kontrolü,
- negatif stok engeli ve rezervasyon çatışması,
- ödeme/e-fatura belirsiz veya başarısız,
- outbox kuyruk yaşı/dead-letter,
- onay SLA ihlali,
- banka mutabakat açık satırı,
- arşiv hash doğrulama sonucu.

İş metriği finansal tutar/PII'yi metric label yapmaz; yüksek cardinality kimliği trace/audit'te tutulur.

## 4. Dashboardlar

1. Yönetici SLO: trafik, hata, latency, availability/error budget.
2. Platform: host/container/DB/disk/network.
3. Finansal işlem sağlığı: postala/outbox/mutabakat.
4. Entegrasyon: e-fatura, banka, e-posta/push.
5. Backup/DR: yaş, WAL, check, restore drill.
6. Güvenlik: login/MFA/admin/export/AV/secret.

Dashboard her panelde veri kaynağı, zaman aralığı ve son güncellemeyi gösterir.

## 5. Alarm tasarımı

- Kullanıcı etkisi/SLO bazlı; yalnız CPU eşiğiyle nöbetçi uyandırılmaz.
- Severity, sahip, runbook linki ve dedup anahtarı vardır.
- Warning iş saatinde görev; critical 7/24 sayfa/telefon politikasına göre.
- Sustained window/flap koruması; fakat audit, backup, e-fatura veri kaybı gibi tek olay kritik olabilir.
- Alarm kanalının kendisi aylık test edilir.

Örnek critical: login tamamen başarısız; postala çift kayıt şüphesi; DB disk %90 ve hızla artıyor; WAL arşivi RPO'yu aştı; backup/restore doğrulaması yok; e-fatura arşiv bozulması; ödeme belirsizliği artışı; audit yazılamıyor.

## 6. Severity ve hedef cevap

| Seviye | Tanım | İlk cevap hedefi |
|---|---|---|
| SEV-1 | yaygın kesinti, veri kaybı/sızıntı, finansal bütünlük riski | 15 dk |
| SEV-2 | önemli modül/entegrasyon kesintisi, güvenli workaround var | 30 dk |
| SEV-3 | sınırlı kullanıcı/işlev, veri bütünlüğü riski yok | iş saatinde 4 saat |
| SEV-4 | iyileştirme/küçük kusur | backlog/SLA |

Hedefler işletmenin vardiya ve destek sözleşmesiyle kesinleşir.

## 7. Müdahale akışı

1. **Tespit/ilan:** incident kimliği, severity, komutan.
2. **Güvenli kıl:** yazma/entegrasyon/hesap izolasyonu; kanıtı koru.
3. **Etki:** kullanıcı, şirket, belge/tarih aralığı, veri/uyum saati.
4. **Teşhis:** son deploy/config, trace/log/metric, DB/queue/dış durum.
5. **Azalt:** feature flag, trafik kesme, roll-forward/rollback veya restore runbook.
6. **Doğrula:** teknik smoke + mali mutabakat + dış durum sorgusu.
7. **İletişim:** düzenli, doğrulanmış durum ve sonraki güncelleme zamanı.
8. **Kapat:** iş sahibi onayı, izleme penceresi, kanıt paketi.
9. **Postmortem:** suçlayıcı olmayan neden/katkı/kontrol ve tarihli eylemler.

Finansal şüphede DB satırı elle silinmez/değiştirilmez. Önce yazma durdurulur, kapsam belirlenir ve ters/düzeltme veya restore kararı yetkilendirilir.

## 8. Hukuki/güvenlik saatleri

Incident kaydı olası kişisel veri, e-fatura veri kaybı/bozulması ve mali rapor etkisini işaretler. Hukuki ekip/mali müşavir bildirim gereği ve süreyi belirler. E-faturadaki olası üç iş günlük süre için otomatik geri sayım ve escalation bulunur.

Şüpheli sızıntıda log temizleme, saldırgan hesabıyla giriş veya etkilenmiş hostta agresif “düzeltme” kanıtı bozabilir; adli koruma ve credential rotasyonu runbook'u uygulanır.

## 9. Runbook kataloğu

- API error/latency artışı,
- PostgreSQL connection/lock/disk/corruption,
- outbox/dead-letter birikmesi,
- e-fatura sonuç belirsizliği/veri bozulması,
- banka ödeme belirsizliği,
- Keycloak/login/MFA kesintisi,
- zararlı dosya/AV bulgusu,
- secret/credential sızıntısı,
- backup/WAL archive başarısızlığı,
- host kaybı/ransomware,
- yanlış mali kayıt/mutabakat farkı.

Her runbook: belirti, güvenli ilk adım, sorgular, yapılmayacaklar, escalation, doğrulama, rollback ve iletişim şablonu.

## 10. Postmortem

Zaman çizelgesi, kullanıcı/iş etkisi, tespit/cevap süreleri, kök ve katkı nedenleri, neden kontrollerin yakalamadığı, iyi çalışanlar, veri/uyum değerlendirmesi ve SMART eylemler. “Kullanıcı hatası” kök neden değildir; guardrail ve sistem koşulu incelenir.

Eylemler issue ID, sahip, son tarih, risk ve doğrulama testi taşır. Benzer sistemlere öğrenim aktarılır; postmortem hassas erişim sınıfındadır.

## 11. Saklama ve erişim

Log/trace/metric/audit birbirinden farklı saklama politikalarına sahiptir. Operasyon logu yasal audit'in yerine geçmez. Erişim rol bazlı ve auditli; yüksek hacimli trace sampling finansal hata/security olayını düşürmeyecek tail/rule stratejisi kullanır.

## 12. Muhasebe/ERP sağlık göstergeleri

Teknik latency yanında:

- posting success/exception/retry ve oldest exception age;
- source event without active posting;
- duplicate active posting/projection generation;
- subledger–GL farkı şirket/defter/para bazında;
- unreconciled statement/payment age ve statement balance farkı;
- unapplied payment, stale allocation ve overdue open item;
- negative stock, valuation repost backlog ve stock–GL farkı;
- GRNI/uninvoiced dispatch/goods-in-transit age;
- period/tax lock override ve reopen;
- sequence gap/void;
- outbox aggregate sequence gap ve dead-letter

metric/exception raporu olarak izlenir. Finansal tutarlar metric label’a konmaz; cardinality ve veri sızıntısı önlenir.

## 13. Posting/reconciliation incident sınıfları

SEV-1 örnekleri: şirketler arası veri sızıntısı, posted veri mutation, widespread unbalanced journal, legal archive loss, dış sisteme duplicate official document/payment. SEV-2: subledger/GL farkı, bank closing mismatch, stuck posting queue, yanlış tax/profile veya backdated valuation spread.

İlk müdahale kanıtı korur:

1. etkilenen company/period/feature için scoped write freeze;
2. source, ledger, generation, release ve rule snapshot;
3. dış provider status ve outbox/inbox kopyası;
4. farkın büyümesini durdurma;
5. uzman/owner ile correction veya projection rebuild kararı.

Doğrudan DB edit veya audit silme incident çözümü değildir. Recovery sonrası golden scenario, control-account/report reconciliation ve kullanıcıya/kuruma bildirim gereği değerlendirilir.

## 14. Trace ve lineage

Correlation chain API command → domain event → subledger → PostingRequest → journal → outbox/provider → report refresh boyunca korunur. Trace sampling sonucu span kaybolsa da business correlation/source ID DB’de kalıcıdır. Hassas payload yerine hash, schema/profile version, result class ve güvenli reference loglanır.
