# Bakım ve Sürüm Yükseltme Planı

## 1. Amaç

İşletim sistemi, container runtime, veritabanı, identity, uygulama ve bağımlılıkları desteklenen/güvenli sürümlerde tutarken mali doğruluk ve kullanılabilirliği korumak.

## 2. Envanter ve sahiplik

Her bileşen için ürün/sürüm/digest, destek bitişi, lisans, sahip, kritik veri, yükseltme yolu ve geri dönüş kaydedilir:

- Ubuntu/kernel/firmware,
- Docker Engine + Compose,
- Caddy,
- PostgreSQL + pgBackRest,
- .NET runtime/SDK/container,
- Keycloak/Java tabanı,
- Node/pnpm/web bağımlılıkları,
- Kotlin/Gradle/Android SDK,
- OTel/monitoring,
- AV/backup ve dış adaptör şemaları.

## 3. Kadans

- Günlük: backup, WAL, disk, cert, queue ve güvenlik alarmı.
- Haftalık: OS/container kritik güncelleme değerlendirmesi, SCA/image scan, hata/kapasite.
- Aylık: planlı patch penceresi, staging doğrulama, restore örneği, erişim istisnaları.
- Üç aylık: bağımlılık toplu güncelleme, tam restore, kapasite ve RLS/authz regresyon.
- Altı aylık: kullanıcı/rol erişim gözden geçirme, secret/sertifika rotasyon planı, DR tabletop.
- Yıllık: destek/EOL, major upgrade yol haritası, veri saklama ve mevzuat gözden geçirme.

Kritik aktif sömürülen açık takvimi hızlandırır; risk bazlı acil yama prosedürü kullanılır.

## 4. Güncelleme süreci

1. Resmi release/security notları ve destek matrisi.
2. Bağımlı bileşen/şema/istemci etkisi.
3. Değişmez artefakt ve SBOM taraması.
4. Staging backup + yükseltme + migration.
5. Auth, finansal golden, entegrasyon, performans ve restore testi.
6. Üretim pencere/onay/iletişim.
7. Preflight ve doğrulanmış yedek.
8. Dağıtım, smoke ve yoğun gözlem.
9. Sürüm envanteri/kanıt ve eski artefakt retention.

Birden çok major bileşen aynı pencerede, arıza kaynağını belirsizleştirecek şekilde yükseltilmez.

## 5. PostgreSQL bakımı

- Minor güncellemeler resmi not ve staging testinden sonra düzenli.
- Major yükseltme: `pg_upgrade` veya logical migration seçimi; extension/collation/driver/backup uyumu.
- Önce/sonra `ANALYZE`, kritik plan karşılaştırma ve mali checksum/mutabakat.
- Autovacuum, bloat, index, statistics, transaction ID yaşı ve uzun transaction izleme.
- `REINDEX`/`VACUUM FULL` bakım penceresi ve disk ihtiyacı olmadan çalıştırılmaz.
- Backup repository ve pgBackRest uyumluluğu major öncesi test edilir.
- PITR/restore yeni sürümde prova edilmeden eski sürüm desteği kaldırılmaz.

## 6. Keycloak bakımı

- Her major için resmi migration/release notes; desteklenen Java/DB.
- Realm/client/auth flow export ve DB yedeği.
- Staging'de web cookie/BFF, Android PKCE, MFA, logout, refresh rotation ve admin.
- Tema/özel provider mümkün olduğunca az; varsa ayrı compatibility test.
- Hostname/proxy header davranışı upgrade sonrası güvenlik testi.
- Admin ve service account erişimleri yeniden gözden geçirilir.

## 7. Uygulama/runtime

- .NET LTS tercih; EOL'dan en az 6 ay önce migration başlamalı.
- NuGet/npm/Gradle bağımlılıkları otomatik öneri, insan review ve lockfile.
- Kırıcı OpenAPI/DB/event değişikliği deprecation penceresi.
- Web browser destek matrisi üç aylık gözden geçirme.
- Android target SDK Google takvimi/dağıtım kanalına göre; min SDK cihaz envanteriyle.
- Feature flag kalıcı config çöplüğüne dönüşmez; sahibi/son tarihi ve kaldırma işi.

## 8. Sertifika ve secrets

- TLS otomatik yenileme; 30/14/7 gün alarm.
- Banka/e-fatura client cert süresi ve manuel onay takvimi.
- DB/service/OIDC/backup secret rotasyonu; çift anahtar penceresi.
- Break-glass credential düzenli doğrulanır fakat normal işte kullanılmaz.
- Rotasyon tatbikatı servis kesintisi ve önceki anahtar iptalini içerir.

## 9. Veri ve saklama bakımı

- Yasal hold/retention gözden geçirme.
- Export/karantina/geçici dosya TTL işi ve raporu.
- Blob orphan ve hash doğrulama; otomatik silme önce dry-run/çift onay.
- Partition/archive yalnız sorgu ve yasal erişimi koruyarak.
- Audit/e-fatura arşivi normal log rotation ile silinmez.

## 10. Kapasite ve kullanım ömrü

Aylık 3/6/12 aylık CPU/RAM/disk/WAL/blob/DB/table büyüme projeksiyonu. Disk artırımı kritik eşiğe gelmeden. UPS/disk SMART ve donanım garantisi; 3–5 yıllık yenileme planı. Tek host yaşlanma veya RTO riski büyürse ikinci host/failover ADR'si.

## 11. Acil güvenlik güncellemesi

- Açığın maruz kalma/varlık/istismar değerlendirmesi.
- Gerekirse geçici WAF/feature disable/egress block.
- Hızlı staging smoke + hedefli regresyon.
- Yetkili acil değişiklik ve geri dönüş.
- Üretim sonrası tarama, IOC kontrolü ve incident kararı.
- 48 saat içinde normal change record/post-review.

## 12. Bakım kabulü

- Sürüm/digest envanteri ve destek tarihleri güncel.
- Backup/restore ve golden mali test başarılı.
- Auth/entegrasyon ve kritik E2E yeşil.
- Alarm/metric normal, queue ve DB planında regresyon yok.
- Release/rollback kaydı tamam.
- Gerekli kullanıcı kesintisi ve değişiklik notu iletilmiş.

## 12. Finansal değişiklik sınıfları

Değişiklikler normal teknik release’den ayrı risk sınıfı taşır:

- posting/tax/rate/rounding rule;
- accounting dimension/control-account mapping;
- inventory cost/valuation logic;
- payment allocation/reconciliation;
- report formula/financial statement mapping;
- official e-Fatura/bank profile;
- period/sequence policy.

Bu sınıflar örnek veriyle old/new accounting impact diff, effective-from, approval, rollback/correction ve historical reproducibility planı ister. Yeni rule eski posted belgeyi sessizce yeniden hesaplamaz.

## 13. Repost gerektiren yükseltme

Kod hatası veya onaylı kural değişimi geçmiş projection’ı etkiliyorsa:

1. source range ve materiality bulunur;
2. legal/tax/closed-period etkisi uzmanla sınıflanır;
3. staging dry-run old/new journal, stock value ve reports diff üretir;
4. gerekiyorsa business correction, değilse projection repost seçilir;
5. çift onay ve bakım penceresi;
6. generation switch, reconciliation ve evidence package;
7. external side-effect olmadığının kanıtı.

“Migration başarılı” bu mali kabulün yerine geçmez.

## 14. Referans kaynak ve profil güncellemesi

KKTC resmi belge, vergi kuralı, UBL profile/code list veya banka statement formatı değişiminde kaynak URL/file hash, retrieval date, competent owner, valid-from ve coexistence window kaydedilir. Parser/validator fixture’ları eski ve yeni sürümü birlikte test eder. Dynamic web sayfası değişti diye production rule otomatik publish edilmez.

Yükseltme sonrası ilk kapanışta exception, control-account farkı, report checksum, allocation/reconciliation ve kullanıcı workaround ölçüleri ayrı gözden geçirilir.
