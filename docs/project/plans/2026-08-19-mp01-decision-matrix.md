# MP-01 Firma Politikaları ve Resmi Kararlar Planı

- **Amaç:** ERP davranışını belirleyen firma politikalarını ve resmi açık soruları sahipli, tarihli ve faz etkisi görülebilir kararlara dönüştürmek.
- **Master fazı ve kapısı:** MP-01 / kritik bilinmeyenlerin sınıflandırılması ve MP-02–MP-03 Definition of Ready değerlendirmesi.
- **Risk sınıfı:** R4 — mevzuat, kişisel veri, finansal politika ve ileride production davranışını etkileyen kararlar.
- **Durum:** in-progress
- **Sahip:** Roller `DEC-MP01-019` gereği atanmadı; isim atamaları geliştirme sonunda yeniden değerlendirilecek.
- **Başlangıç / hedef tarih:** 2026-08-19 / sahip ve hedef tarihler atandıktan sonra belirlenecek.
- **İlgili requirement aileleri:** ARCH, DATA, IAM, ORG, PARTY, INV, TRY, INS, GL, TAX, EINV, WF, SEC, OPS, DR, REL.
- **Etkilenen belgeler/modüller:** Organizasyon, IAM, muhasebe, cari, stok, banka, iş akışı, vergi/e-Fatura, güvenlik, operasyon ve restore.
- **Okunan zorunlu belgeler:** `AGENTS.md`, `MASTER_PLAN.md`, `PLANS.md`, `docs/README.md`, `docs/00-foundation/01-product-scope-and-principles.md`, `docs/00-foundation/04-data-architecture.md`, `docs/legal/01-kktc-legal-matrix.md`, `docs/legal/02-official-approvals-and-open-questions.md`, `docs/modules/01-identity-access.md`, `docs/modules/02-organization-master-data.md`, `docs/modules/03-party-current-accounts.md`, `docs/modules/04-items-inventory.md`, `docs/modules/09-accounting-general-ledger.md`, `docs/modules/12-workflow-approvals.md`, `docs/quality/02-security-and-threat-model.md`, `docs/operations/02-backup-restore-disaster-recovery.md`.
- **Definition of Ready sonucu:** conditional. Karar keşfi ve geri döndürülebilir MP-02 bootstrap hazırlıkları yapılabilir; isimli sahip, firma topolojisi ve finansal politikalar tamamlanmadan MP-01 çıkış kapısı veya MP-03 uygulaması hazır değildir.

## Master plan ilişkisi

Bu plan, ilk uygulama backlog'unun 1–3 numaralı maddelerini yürütür:

1. Karar sahiplerini atamak.
2. Resmi ve hukuki açık soruları blocking/non-blocking sınıflandırmak.
3. Şirket, şube, dönem, para, stok değerleme ve onay politikalarını kaydetmek.

Planın çıktıları MP-02 repository bootstrap planının güvenli varsayım sınırını ve MP-03 muhasebe çekirdeğinin gerçek Definition of Ready sonucunu belirler. Bir faz durumu veya kapısı yalnız kanıt oluştuğunda `MASTER_PLAN.md` içinde güncellenecektir.

## Bağlam

Repository; v1.2 şartname paketi ile birlikte .NET backend, strict web, Android/Compose, PostgreSQL migration/RLS, Keycloak auth, audit/outbox, restore testleri ve CI içeren MP-02 teknik platformunu taşır. Commit `2f4d4ee` için GitHub Actions run `32360372748` içindeki altı job'ın tamamı geçmiştir. Gerçek firma yapısı, rol sahipleri, muhasebe politikaları, veri konumu ve resmi onayların çoğu açık durumdadır.

Git çalışma kökü `Kagu ERP` klasörüdür; bağımsız repository `main` dalı ve `https://github.com/KaguLtd/Kagu-ERP.git` origin'i kullanır. Repository sınırı `DEC-MP01-018` ile çözülmüştür.

## Kapsam

### Dahil

- Karar sahibi rollerinin ve isim bekleyen alanların kaydı.
- Firma politikaları için tekil karar kimlikleri, durum, kanıt ve faz etkisi.
- Hukuki/resmi soruların hangi faz veya feature'ı bloke ettiğinin sınıflandırılması.
- MP-02 ve MP-03 için ayrı Definition of Ready sonucu.
- Cevap beklerken güvenle yürütülebilecek geri döndürülebilir teknik işlerin sınırı.

### Dahil değil

- Mevzuat, oran, resmi format veya kurum cevabı uydurmak.
- Muhasebe hesabı, stok değerleme yöntemi veya kur kaynağını kullanıcı/uzman kararı olmadan seçmek.
- Production, DNS, firewall, secret, banka veya e-Fatura sisteminde değişiklik.
- Repository ve uygulama iskeletini bu plan kapsamında kurmak; bu MP-02 planının işidir.

## Değişmezler ve güvenlik sınırları

- Finansal invariant: Karar eksikliği, decimal para, dengeli fiş, append-only posting, idempotency, control-account mutabakatı veya kapalı dönem kurallarını gevşetmez.
- Yetki/scope: Tenant/company/branch/warehouse/bank sahipliği açıklığa kavuşmadan gerçek veri modeli veya geniş scope varsayımı production-ready sayılmaz.
- Kişisel veri: Veri konumu ve dış transfer kararı olmadan gerçek kişisel veri dış servise gönderilmez.
- Mevzuat: KDV, tax point, e-Fatura profili, yasal saklama ve resmi entegrasyon doğrulanmadan production kuralı yayımlanmaz.
- Geriye uyumluluk: Bu plan kod veya şema üretmez.
- Veri kaybı riski: Bu görev yalnız doküman ekler; üretim veya kullanıcı verisine dokunmaz.

## Sınıflandırma modeli

Her karar şu durumlardan birini kullanır:

- `open`: Cevap veya kanıt yok.
- `requested`: Yetkili kişi/kuruma soru iletildi.
- `evidence-received`: Yazılı kanıt alındı, etki analizi bekliyor.
- `approved`: Yetkili sahip kararı yayımladı.
- `superseded`: Yeni sürümlü kararla değiştirildi.

Blokaj etkisi karar bazında ayrıca yazılır:

- `MP-02`: Temel platform veya güvenlik iskeleti başlayamaz.
- `MP-03`: Muhasebe çekirdeğinin gerçek iş davranışı başlayamaz.
- `feature`: İlgili modülün gerçek davranışı/entegrasyonu etkinleşemez.
- `production`: Teknik geliştirme yapılabilir; canlı kullanım açılamaz.
- `non-blocking`: Geri döndürülebilir, sentetik veya fake-adapter teknik iş güvenle ilerleyebilir.

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | Karar kayıt şeması ve başlangıç matrisi | Tüm MP-01 minimum karar alanları kimlik, sahip rolü, durum ve blokaj taşır | completed |
| 2 | Resmi/hukuki sınıflandırma | Her soru grubu feature/production etkisi ve güvenli paralel iş sınırı taşır | completed |
| 3 | Repository sınırı kararı | Bağımsız repository, `main` dalı ve GitHub `origin` doğrulanmıştır | completed |
| 4 | İsimli sahip ataması | Ürün, teknik, muhasebe, güvenlik/veri ve operasyon sorumluları kayıtlıdır | pending — `DEC-MP01-019` ile geliştirme sonuna ertelendi |
| 5 | Firma topolojisi ve temel politikalar | Tenant/company/branch/period/currency/approval kararları kanıtlıdır | pending |
| 6 | Finansal çekirdek politikaları | Hesap planı, allocation, kur/yuvarlama, kilit ve kontrol hesabı kararları onaylıdır | pending |
| 7 | MP-02 ve MP-03 DoR değerlendirmesi | Her eksik madde owner/date ve açık blokajla raporlanır | in-progress — MP-02 teknik final pass; MP-03 yalnız politika bağımsız spike, business kararları bekliyor |

## Test planı

- Doküman: Yerel Markdown linkleri ve referans verilen yollar doğrulanır.
- Tutarlılık: Her `approved` kararın authority, evidence, effective date ve review date alanı dolu olmalıdır.
- Güvenlik: Secret, gerçek VKN/IBAN, kişisel veri veya kurum credential'ı dokümana yazılmaz.
- Faz kapısı: MP-01 tamamlandı veya MP-03 ready ifadesi, eksik kritik karar varken kullanılamaz.
- Uygulanmaz: Kod, DB, API, E2E, migration ve restore testi bu doküman-only görevde çalıştırılmaz.

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|
| 2026-08-19 | İsimli karar sahipleri bilinmiyor | MP-01 çıkış kapısı geçemez | Kullanıcı/ürün sahibi atayacak |
| 2026-08-19 | Git kökü başlangıçta proje üst klasöründeydi | `DEC-MP01-018` ile bağımsız repository oluşturularak risk giderildi | Yetkili kullanıcı; isimli teknik/ürün sahibi teyidi bekliyor |
| 2026-08-19 | Resmi ve finansal kararların çoğu açık | Gerçek vergi/e-Fatura, GL, stok ve production davranışı etkinleşemez | İlgili kurum/uzman/firma sahibi |
| 2026-08-19 | Teknik temel geri döndürülebilir biçimde kurulabilir | MP-01 cevapları beklerken MP-02'nin sınırlı işleri ilerleyebilir | Master planın paralel ilerleme kuralı |
| 2026-08-21 | Kullanıcı isimli sahipleri geliştirme sonuna erteledi | MP-01 çıkış kapısı, production ve uzman mali kabulü bloklu kalır | `DEC-MP01-019`; yalnız politika bağımsız ve geri döndürülebilir teknik işler ilerler |

## İlerleme günlüğü

### 2026-08-19

- Repository'deki 57 Markdown dosyasının tamamı okundu; alt klasörde ek `AGENTS.md` bulunmadığı doğrulandı.
- MP-01 R4 olarak sınıflandırıldı.
- Başlangıç karar kayıt dosyası ve resmi açık soru blokaj matrisi oluşturuldu.
- MP-02 ve MP-03 Definition of Ready sonucu `conditional` olarak kaydedildi.
- `DEC-MP01-018` yetkili kullanıcı kararıyla `approved` yapıldı.
- `Kagu ERP` klasörü bağımsız Git repository olarak `main` dalıyla başlatıldı ve `origin` olarak `https://github.com/KaguLtd/Kagu-ERP.git` bağlandı.
- Remote'un erişilebilir ve boş olduğu `git ls-remote` ile doğrulandı; commit veya push yapılmadı.
- Sıradaki kesin adım: MP-02 repository-bootstrap görev planını oluşturmak; isimli sahip ve firma politikası toplama işi MP-01 içinde paralel devam edecek.

### 2026-08-20

- MP-02 teknik platformu yerel doğrulama ve GitHub Actions run `32360372748` ile geçti; clean bootstrap, backend/format, web, Android, secret scan ve PostgreSQL migration/RLS/restore job'larının 6/6'sı başarılıdır.
- MP-02 kurumsal kapanışı, isimli teknik/güvenlik/operasyon sahipleri ve `DEC-MP01-017` production RPO/RTO kabulü olmadan tamamlanmış sayılmadı.
- MP-03 Definition of Ready sonucu değişmedi: `DEC-MP01-001`–`010`, `012` ve ilgili isimli sahipler onaylanmadan gerçek accounting-kernel davranışı blokludur.
- Sıradaki kesin adım: beş sorumlu rol için isim ataması ve karar başına hedef tarih kaydıdır.

### 2026-08-21

- Kullanıcı, beş sorumlu rolün şimdilik `atanmadı` kalmasını ve geliştirme sonunda yeniden değerlendirilmesini istedi; karar `DEC-MP01-019` olarak kaydedildi.
- MP-02 teknik çıkış kapısı tamamlandı. İsim eksikliği artık MP-02 teknik kapanışını geri açmaz; production ve uzman kabulünü bloklamaya devam eder.
- MP-03 business implementation için `DEC-MP01-001`–`010` ve `012` blokajları korunurken, decimal ve dengeli journal gibi politika bağımsız invariantları kapsayan teknik spike başlatıldı.

## Tamamlanma kanıtı

- [x] Karar kayıt şeması ve başlangıç karar listesi.
- [x] Blocking/non-blocking sınıflandırma modeli.
- [x] Resmi soru gruplarının faz/feature etkisi.
- [x] Bağımsız Git repository sınırı ve remote bağlantısı.
- [ ] İsimli karar sahipleri.
- [ ] Firma ve finans politikalarının yazılı kanıtı.
- [ ] MP-02 ve MP-03 Definition of Ready nihai sonucu.
- [ ] Açık soruların hedef tarihleri.
- [ ] MP-01 çıkış kapısı değerlendirmesi.
