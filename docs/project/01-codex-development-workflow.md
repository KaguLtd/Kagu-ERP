# Codex ile Geliştirme İş Akışı

## 1. Amaç

Bu doküman, şartname setini Codex'in güvenli ve küçük adımlarla çalışan yazılıma dönüştürmesi için görev düzeyi çalışma protokolüdür. Root [AGENTS.md](../../AGENTS.md) bağlayıcı kuralları, [MASTER_PLAN.md](../../MASTER_PLAN.md) program fazlarını ve belge yönlendirmesini, [PLANS.md](../../PLANS.md) ise karmaşık görev planının biçimini taşır.

OpenAI'nin Codex rehberindeki temel yaklaşım uygulanır: görevde amaç, bağlam, kısıtlar ve “tamamlanmış” ölçütü açık olmalı; uzun iş için plan tutulmalı; değişiklik test edilip gözden geçirilmelidir.

## 2. Talimat katmanları

Codex, repo kökündeki `AGENTS.md` ve `MASTER_PLAN.md` ile başlar. İleride `apps/web/AGENTS.md`, `apps/android/AGENTS.md`, `src/Modules/Accounting/AGENTS.md` gibi daha yakın dosyalar yalnız kendi alt ağacı için daha özel talimat verebilir.

Kurallar:

- Root dosya kısa ve evrensel kalır; ayrıntıya link verir.
- Yakın `AGENTS.md` üst kuralı sessizce gevşetmez; zorunlu istisna ADR ister.
- Aynı kuralı çok dosyada kopyalamak yerine tek kaynak kullanılır.
- Talimat dosyaları büyüdükçe Codex bağlamını boğmamak için görev rotası docs indeksinden seçilir.
- Koddaki fiilî davranış ile doküman çelişirse Codex varsayım yapmaz; farkı raporlar ve karar ister.

## 3. Görev boyutu

Bir görev tek bir doğrulanabilir sonuç üretmelidir. Uygun örnek:

> “Cari liste API'sini company scope, cursor pagination ve yetki testleriyle uygula; OpenAPI ve docs'u güncelle.”

Uygun olmayan örnek:

> “Tüm ERP'yi yap.”

Bir dikey dilim ideal olarak domain → DB migration → API → web/mobil gereği → audit/telemetry → test → docs zincirini tamamlar. Bir PR birden fazla bağımsız modülü ve büyük refactor'ı karıştırmaz.

## 4. Her görev öncesi bağlam paketi

Codex şu sırayı izler:

1. `AGENTS.md` ile master plandaki güncel faz, risk ve yönlendirme bölümü.
2. [Docs indeksi](../README.md) üzerinden ilgili temel doküman: veri/API/repo/ortak akış.
3. Davranışın sahibi modül şartnamesi.
4. İstemci, test, güvenlik, hukuk veya operasyon dokümanı gerekiyorsa yalnız ilgili bölüm.
5. Mevcut kod, test, migration, görev planı ve ADR'ler.
6. Çalışma ağacındaki kullanıcı değişiklikleri; ilgisiz değişikliklere dokunmama.

Sonra varsayımlar, kapsam dışı ve acceptance ölçütleri plana yazılır.

## 5. Plan kullanımı

Master plandaki görev planı ölçütlerinden biri oluştuğunda `PLANS.md` biçiminde yaşayan uygulama planı gerekir. Plan, ilgili MP fazını ve ilerlettiği kapıyı başlığında taşır.

Plan:

- kullanıcı sonucu ve mevcut durumu,
- dosya/kod keşfini,
- küçük kilometre taşlarını,
- finansal/uyum risklerini,
- test/kanıt komutlarını,
- rollback ve doküman güncellemesini,
- karar günlüğünü

içerir. Plan kodla birlikte güncellenir; yalnız başlangıç niyeti değildir.

## 6. Geliştirme döngüsü

1. **Keşfet:** `rg`, testler ve mevcut patterns; isim tahmin etme.
2. **Modelle:** invariant, state machine, yetki ve transaction sınırı.
3. **Planla:** en küçük dikey dilim ve acceptance.
4. **Uygula:** domain'den dışa doğru; gereksiz altyapı ekleme.
5. **Test et:** önce değişen riskin dar hedef testi; tam repository/DB/restore/client regresyonunu `DEC-MP01-024` uyarınca MP validating/kapanış paketinde çalıştır.
6. **İncele:** diff, secret/PII, tenant scope, mali/audit/outbox etkisi.
7. **Doğrula:** API/OpenAPI, migration, web/mobil ve operasyon smoke.
8. **Belgele:** gereksinim, ADR, runbook ve plan durumu.
9. **Teslim et:** ne değişti, kanıt, açık risk ve sonraki güvenli adım.

## 7. Önce test / invariant yaklaşımı

Finansal veya yetki değişikliğinde önce beklenen değişmez kural ve başarısız örnek yazılır. Örnekler:

- aynı `Idempotency-Key` ikinci fiş üretmez,
- kesinleşmiş fiş güncellenemez,
- başka şirket kimliğiyle ID tahmini 404/403 politikasıyla reddedilir,
- e-fatura sonucu bilinmiyorsa tekrar gönderim yapılmaz,
- paralel stok rezervasyonu miktarı aşamaz.

UI screenshot başarısı domain invariant kanıtı değildir.

## 8. Migration disiplini

- Migration elle gözden geçirilir; uygulama açılışında otomatik kontrolsüz schema update yok.
- Yeni alan önce nullable/default/backfill planıyla; büyük tabloda lock süresi testli.
- Constraint, önce bozuk veriyi raporlayan/temizleyen aşamadan sonra etkinleştirilir.
- Production veri dönüşümü idempotent ve yeniden başlayabilir.
- Migration ile model snapshot, SQL ve rollback/roll-forward dokümanı birlikte.
- Eski Android/web API uyumluluğu release dokümanına göre korunur.

## 9. Kod inceleme kontrolü

Codex kendi diff'ini şu sırayla okur:

- şartname/acceptance karşılanıyor mu,
- yanlış şirket/şube verisi sızabilir mi,
- finansal kayıt çift/sessiz değişebilir mi,
- transaction ve outbox atomik mi,
- authz endpoint/query/export'ta mı,
- para/tarih/kur/yuvarlama doğru tip mi,
- secret/PII log veya hata mesajında mı,
- concurrency/idempotency test edilmiş mi,
- migration ve API geriye uyumlu mu,
- docs/telemetry/runbook güncel mi.

## 10. Ne zaman durup soru sorulur

Codex aşağıdakileri tahmin etmez:

- KKTC oranı, beyan biçimi veya resmi onay sonucu,
- geçmiş mali veriyi değiştirecek karar,
- production secret veya yedek silme,
- dış sisteme gerçek fatura/ödeme gönderme,
- belirsiz şirket/şube veri sahipliği,
- veri transferi/ülke seçimi,
- kullanıcı değişikliklerini yok edecek git/dosya işlemi,
- kabul ölçütünü kökten değiştiren ürün tercihi.

Blokaj raporu: bilinen, bilinmeyen, neden önemli, güvenli seçenekler ve önerilen varsayılan.

## 11. Branch ve çalışma ağacı

- Her görev ayrı branch; isim `feat/party-search`, `fix/einvoice-idempotency` gibi.
- Kirli worktree kullanıcıya ait olabilir; ilgisiz dosya düzeltilmez/silinmez.
- Küçük, anlamlı commit; generated dosya ve migration açıkça belirtilir.
- Merge öncesi rebase/merge ekip politikasına göre; history rewrite izinsiz değil.
- Release tag, image digest ve migration manifesti eşlenir.

## 12. Örnek Codex görevi

```text
Amaç: INV-RES-001 stok rezervasyonu invariant'ını uygula.

Bağlam:
- AGENTS.md
- docs/00-foundation/04-data-architecture.md
- docs/modules/04-items-inventory.md
- docs/quality/01-testing-and-quality-strategy.md

Kısıtlar:
- PostgreSQL constraint/transaction kullan.
- Web veya Android doğrudan DB erişmez.
- Kesinleşmiş hareketi güncelleme.
- Başka şirket verisini açığa çıkarma.

Tamamlanmış:
- Paralel iki rezervasyon testi gerçek PostgreSQL'de.
- API ProblemDetails ve OpenAPI güncel.
- Audit/outbox olayı var.
- İlgili testler ve tam backend test paketi yeşil.
- ADR gerekmediyse neden gerekmediğini teslim notunda belirt.
```

## 13. İlk geliştirme sırası

1. Repo/toolchain/CI ve local Compose.
2. Identity + company scope + RLS spike ve restore smoke.
3. Organization/master data ile dönem bağlamı.
4. Muhasebe event/posting çekirdeği ve dengeli fiş.
5. Cari vade/açık kalem + ödeme + allocation + banka reconciliation ilk dikey dilimi.
6. Cari ekstre/aging ve subledger–GL kontrol mutabakatı.
7. Stok ile satış siparişi→rezervasyon→sevk→fatura.
8. Satın alma/banka/kasa/çek ve onay akışları.
9. KKTC vergi/e-Fatura; resmi onay paralel iş akışı.
10. Üretim web olgunlaştırma, Android dar pilot, migrasyon ve go-live.

Her adım öncekinin gözlemlenebilir, yedeklenebilir ve testli platformunu korur.

## 14. Araştırmadan koda geçiş protokolü

ERP özelliği yazmadan önce Codex:

1. İlgili süreç sahibi ve sözlük terimlerini belirler.
2. Kaynakçadaki birincil standart/ders ve en az bir olgun açık kaynak davranışını inceler.
3. Gözlenen davranışı kopyalamadan “risk/invariant → proje kararı → kabul/red gerekçesi” olarak yazar.
4. BPMN düzeyinde happy path, exception ve telafiyi çıkarır.
5. REA tablosunda commitment, economic event, resource ve agent’ı tanımlar.
6. Accounting impact ve control/reconciliation matrisini doldurur.
7. Küçük vertical slice’ı API, DB, audit, report ve testle uygular.

Başka ERP’nin source code veya schema adı proje modeline aynen taşınmaz. Lisans, clean-room ve KKTC farkı incelenir. Ürün davranışının varlığı gereksinim değildir; orta ölçekli firma değeri ve operasyon sahipliği yoksa ertelenir.

## 15. Accounting kernel spike

Satış/stok genişlemeden önce ayrı ama atılabilir bir spike şu davranışları kanıtlar:

- kaynak economic event + idempotent posting request;
- dengeli, source-linked, append-only journal;
- due schedule/open item + payment allocation/unallocation;
- statement line + reconciliation;
- subledger/control-account cross-foot;
- reversal ile business correction;
- dry-run + generation ile projection repost;
- as-of report ve source drill-down.

Spike production shortcut’u değildir; ADR/veri/API/test kararlarını doğrular ve temiz uygulamaya yön verir.

## 16. İnceleme kontrolü

PR review’de yalnız diff değil:

- state transition ve allowed actions;
- effective/recorded/posted dates;
- source/subledger/allocation/GL lineage;
- closed period/tax/inventory impact;
- duplicate/retry/crash behavior;
- report/control-account effect;
- permission/scope/SoD;
- migration/restore/rebuild

incelenir. Belirsiz mali yorum yetkili uzman kararına açık soru olarak gider.
