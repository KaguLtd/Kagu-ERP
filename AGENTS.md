# AGENTS.md — KKTC ERP çalışma kuralları

Bu dosya repository genelindeki tüm Codex görevleri için bağlayıcıdır. Program sırası ve belge yönlendirmesi `MASTER_PLAN.md`, ayrıntılı sözleşmeler `docs/` altındadır; bu dosyayı gereksiz büyütme. Bir alt klasörde daha özel bir `AGENTS.md` varsa o klasördeki kurallar buna eklenir ve çelişkide daha özel kural geçerlidir.

## 1. Amaç

KKTC'de orta ölçekli işletmeler için güvenli, denetlenebilir, web ve Android erişimli ERP geliştir. Bir özelliği yalnız ekranda çalıştığı için tamamlanmış sayma; stok, cari, muhasebe, yetki, audit ve mevzuat etkisini birlikte doğrula.

## 2. Göreve başlamadan önce

1. Bu dosyayı ve `MASTER_PLAN.md` içindeki güncel faz, görev yönlendirme ve kapı kurallarını oku. Paket ilk kez görülüyorsa veya sürüm değişmişse `README.md` dosyasını da oku.
2. İsteği master fazı, requirement kimliği, iş alanı ve risk sınıfına bağla.
3. `docs/README.md` ve master yönlendirme matrisi üzerinden yalnız ilgili temel, modül ve çapraz belgeleri seç.
4. İlgili fazın giriş kapısını kontrol et. Karmaşık işte `PLANS.md` formatında master fazına bağlı plan oluştur veya mevcut planı güncelle.
5. Belirsiz muhasebe, vergi, veri silme, erişim kapsamı veya resmi entegrasyon kuralında varsayım yapma; dur ve karar iste.
6. Değişiklikten önce mevcut kodu, testleri, migration'ları, ADR'leri ve kullanıcıya ait değişiklikleri incele.
7. Kullanıcı yalnız devam et derse `MASTER_PLAN.md` içindeki en düşük numaralı, tamamlanmamış ve blokajsız işten ilerle.

## 3. Değişmez finansal kurallar

- Para için binary floating point (`float`, `double`) kullanma. `decimal` ve PostgreSQL `numeric` kullan.
- Kesinleşmiş `journal_entry` için toplam borç toplam alacağa eşit olmalı.
- Kesinleşmiş belge, cari hareket, stok hareketi, banka hareketi ve muhasebe satırı `UPDATE`/`DELETE` ile düzeltilmez; iptal, ters kayıt veya karşı belge oluşturulur.
- Bir kaynak belge yalnız bir aktif posting sonucu üretir; tekrar çağrı idempotent olmalıdır.
- Settlement, ödeme veya açık kalem kalanını aşamaz.
- Stok transferi toplam miktarı korur; kaynak çıkışı ile hedef girişi aynı transfer kimliğine bağlıdır.
- Kapalı döneme işlem yazılmaz. Yeniden açma çift onay, gerekçe ve audit ister.
- KDV ve kur hesabı kullanılan kural/kur sürümüyle yeniden üretilebilir olmalıdır.
- Kaynak belge/ekonomik olay, alt defter, allocation, banka reconciliation ve GL kaydını tek tablo veya tek “ödendi” alanında birleştirme.
- Ödeme/tahsilat nakit hareketidir; allocation hangi açık kalemi kapattığını gösterir. Allocation kaldırmak ödeme veya GL hareketini silmez.
- Kaynak olayın düzeltmesi reversal/correction ile yapılır. Repost yalnız türetilmiş ledger/projection’ı aynı kaynak ve kural snapshot’ından yeniden kurabilir; yeni ticari gerçek üretemez.
- Her hareket document/effective date ile recorded/posted timestamp’ini ayrı taşır. Backdate; dönem, vergi ve stok değerleme etkisini yeniden değerlendirir.
- Alt defter toplamı ilgili GL kontrol hesabına aynı şirket, para, tarih kesimi ve boyutlarda eşit olmalıdır.

## 4. Yetki ve veri izolasyonu

- Her iş tablosunda uygun olduğu yerde `tenant_id` ve `company_id` zorunludur.
- Her endpoint permission + scope + koşul denetimi yapar. UI öğesini gizlemek yetkilendirme değildir.
- Başka şirket/şube/depo/banka hesabına ait ID verilerek veri okunamadığını negatif testle kanıtla.
- PostgreSQL RLS savunma katmanıdır; uygulama filtresinin yerine geçmez. Uygulama DB rolü tablo sahibi veya `BYPASSRLS` olamaz.
- Hazırlayan kendi kritik işlemini onaylayamaz. SoD kuralları `workflow-approvals.md` ile uyumlu olmalıdır.
- Web ve Android doğrudan PostgreSQL'e bağlanamaz; DB portu dış ağa açılamaz.
- Quorum isteyen onayda aynı kişi birden fazla gerekli oyu dolduramaz; delegation bu ayrımı baypas edemez.

## 5. Mimari sınırlar

- Başlangıçta modüler monolit kullan. Yeni mikroservis, message broker, Kubernetes veya Redis eklemek için ölçülmüş ihtiyaç ve ADR gerekir.
- Modüller başka modülün tablosuna doğrudan yazamaz. Okuma da yayımlanmış application contract/read model üzerinden olmalıdır.
- İç finansal kesinleştirme aynı PostgreSQL transaction'ında güçlü tutarlı; dış entegrasyonlar transactional outbox ile asenkron olmalıdır.
- Tam event sourcing uygulama. Finansal ve operasyonel hareketler append-only olabilir; varlıkların güncel projeksiyonu ayrıca tutulabilir.
- API tabanı `/api/v1`; yazma işlemlerinde idempotency, eşzamanlılık sürümü ve Problem Details standardı uygulanır.
- Android ve web için aynı iş kuralını istemcide yeniden yazma; server tek otoritedir.

## 6. Kod ve repository kuralları

- Backend: .NET 10 LTS, nullable ve analyzers açık, warnings-as-errors CI'da açık.
- Web: TypeScript `strict`, React; server state TanStack Query, formlar React Hook Form + Zod; iş verisini rastgele global store'a kopyalama.
- Android: Kotlin, Compose, coroutines/Flow, repository katmanı, Room ve WorkManager; system browser + PKCE.
- Veritabanı migration'ları ileri uyumlu `expand → migrate → contract` yaklaşımıyla yazılır. Production'da uygulama açılışında otomatik migration çalışmaz.
- Sırlar repository'ye, örnek loglara, fixture'lara veya istemci bundle'ına yazılmaz.
- Tarih/saat DB'de UTC `timestamptz`; yasal belge tarihi ayrıca `date` ve `Europe/Nicosia` bağlamıyla tutulur.
- Yeni bağımlılık eklerken lisans, bakım durumu, güvenlik ve mevcut yığınla gereklilik değerlendirmesi yap; gerekçeyi PR/ADR'ye yaz.

## 7. Test kapıları

Her davranış değişikliğinde ilgili seviyeleri çalıştır:

- Domain unit testleri.
- PostgreSQL üzerinde gerçek DB integration testleri; salt mock yeterli değildir.
- API contract ve authorization negatif testleri.
- Finansal invariant/property testleri.
- Web için component ve kritik akış Playwright testleri.
- Android için repository/ViewModel ve kritik Compose UI testleri.
- Migration ileri/geri uyumluluk ve boş/verili DB testi.
- Kaynak olay → alt defter → GL posting ve rapor cross-foot golden senaryosu.
- Taksitli/kısmi allocation, unallocation, banka kesinleşmesi ve fazla/avans ödeme testi.
- Geçmiş tarihli stok hareketi, değerleme zinciri, kontrollü repost ve kapanış cut-off testi.
- Rapor toplamından kaynak belgeye drill-down ve aynı as-of kesimde yeniden üretim testi.

Bir finansal veya güvenlik testi atlanırsa nedenini ve riski açıkça raporla. Testi yalnız geçsin diye zayıflatma, silme veya production davranışını test verisine özel değiştirme.

## 8. Güvenlik ve mevzuat

- Hedef OWASP ASVS 5.0 Level 2'dir; finansal yönetici ve sistem yönetimi yollarında ek Level 3 kontrolleri değerlendirilir.
- Web tokenlarını `localStorage`/`sessionStorage` içinde tutma. Web aynı-origin cookie/BFF modeli; mobil PKCE ve güvenli cihaz saklama kullanır.
- Hassas veriyi loglama; VKN, IBAN, adres, token ve belge içeriğini maskele.
- Dosya yüklemesinde MIME, uzantı, boyut, zararlı içerik, dosya adı ve tenant kapsamı doğrulanır.
- KKTC dışına kişisel veri/backup/log gönderme kararı, veri transferi uygunluğu yazılı onaylanmadan uygulanmaz.
- KDV oranı, resmi belge şekli veya e-Fatura sözleşmesi hard-code edilmez; güncel resmi kaynağa bağlı sürümlü kuraldır.

## 9. Değişiklik akışı

1. Goal, master fazı, risk sınıfı, kapsam, sınırlar ve `done when` maddelerini yaz.
2. İlgili requirement ID'lerini, okunan sözleşmeleri ve faz kapısını belirt.
3. `MASTER_PLAN.md` Definition of Ready kontrolünden sonra küçük bir dikey dilim uygula.
4. Test/migration/OpenAPI/audit/observability/doc değişikliklerini aynı işte tamamla.
5. Diff'i güvenlik, tenant sızıntısı, finansal invariant, restore ve geriye uyum açısından incele.
6. Çalıştırılan komutları ve sonuçları teslim notuna yaz.
7. Görev planını her çalışmada; master planı yalnız faz veya kapı kanıtı değiştiğinde güncelle.
8. Davranış veya karar değiştiyse ilgili MD/ADR/legal kaydını güncelle.

## 10. Bitti tanımı

Bir iş ancak şu koşullarda biter:

- Kabul kriterleri ve ilgili testler geçer.
- Yetki, scope, audit, hata ve idempotency davranışı tanımlıdır.
- DB migration ve rollback/compensation planı vardır.
- OpenAPI ve istemci sözleşmesi günceldir.
- Gözlemlenebilirlik için log/metric/trace veya gerekçeli istisna vardır.
- Güvenlik ve kişisel veri etkisi incelenmiştir.
- İlgili belgeler güncellenmiş ve açık soru bırakılmışsa kayıt altına alınmıştır.
- Bağlı görev planı günceldir; bir master kapısı ilerlediyse kanıtı `MASTER_PLAN.md` içinde kayıtlıdır.

## 11. Yasaklar

- Production DB'yi veya volume'u silen komut çalıştırma.
- Kullanıcı onayı olmadan production, DNS, firewall, sertifika, secret, banka veya e-Fatura sisteminde dış değişiklik yapma.
- Finansal doğruluk veya mevzuat belirsizliğini sessiz varsayımla kapatma.
- Doğrudan DB erişimli mobil/web uygulaması geliştirme.
- Kesinleşmiş kayıtları yerinde değiştiren admin aracı oluşturma.
- Test, lint, typecheck veya güvenlik kapısını devre dışı bırakıp işi tamamlandı sayma.
- Posted satırı “repost” adı altında silip yeniden yazarak audit zincirini kaybettirme.
- İstemci görünürlüğünü, kayıt/buton/komut yetkisinin yerine koyma.
