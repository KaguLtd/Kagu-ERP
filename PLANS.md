# Yürütme Planı Standardı

Bu dosya tek bir karmaşık işin uygulama planı standardıdır ve kök [MASTER_PLAN.md](MASTER_PLAN.md) içindeki program sırasına tabidir. Bir iş bir günde bitmeyecekse, birden fazla modülü etkiliyorsa, migration; finansal, güvenlik, mevzuat, dış entegrasyon, backup/restore veya pahalı geri dönüş riski taşıyorsa bu formatla `docs/project/plans/<YYYY-MM-DD>-<slug>.md` oluşturulur. Plan yaşayan belgedir; her Codex çalışmasında güncellenir.

## Plan başlığı

- **Amaç:** Kullanıcının görebileceği sonuç.
- **Master fazı ve kapısı:** MP-XX / giriş veya çıkış kapısı.
- **Risk sınıfı:** R0 / R1 / R2 / R3 / R4.
- **Durum:** proposed / ready / in-progress / blocked / validating / completed / superseded.
- **Sahip:** Ürün, teknik ve muhasebe sorumluları.
- **Başlangıç / hedef tarih:**
- **İlgili requirement ID'leri:**
- **Etkilenen belgeler/modüller:**
- **Okunan zorunlu belgeler:**
- **Definition of Ready sonucu:**

## Master plan ilişkisi

Plan; bağlı olduğu master fazının giriş koşulunu, ilerlettiği çıkış kanıtını ve önceki bağımlılıkların durumunu açıklar. Görev planı modül davranışını veya program sırasını sessizce değiştiremez. Yeni mimari karar ADR'ye, resmi/mevzuat kararı legal kayda, faz kapısı değişikliği MASTER_PLAN.md dosyasına yazılır.

## Bağlam

Mevcut davranışı, problemi, ilgili dosyaları ve neden şimdi yapıldığını kısa anlat.

## Kapsam

### Dahil

- ...

### Dahil değil

- ...

## Değişmezler ve güvenlik sınırları

- Finansal invariant:
- Yetki/scope:
- Kişisel veri:
- Mevzuat:
- Geriye uyumluluk:
- Veri kaybı riski:

## Tasarım

- İş süreci/BPMN: başlangıç, bitiş, aktör/swimlane, karar, exception ve telafi.
- REA özeti: taahhüt, ekonomik olay, kaynak, iç/dış aktör ve belge referansları.
- Domain değişikliği:
- Veritabanı/migration:
- Kaynak olay–alt defter–allocation–GL etkisi ve posting rule snapshot:
- Etkin tarih, kayıt tarihi, cut-off, dönem/vergi/stok değerleme etkisi:
- API ve olaylar:
- Web:
- Android:
- Audit/observability:
- Raporlar, kontrol hesapları ve mutabakat:
- İç kontroller: risk, owner, önleyici/tespit edici, sıklık, kanıt, exception:
- Deployment/rollback:

## Milestone'lar

| No | Dikey dilim | Doğrulama | Durum |
|---:|---|---|---|
| 1 | ... | ... | pending |

## Test planı

- Unit:
- Property/invariant:
- DB integration:
- Contract:
- E2E:
- Security:
- Performance:
- Migration/restore:
- Golden accounting cycle ve report cross-foot:
- Backdate/repost ve kapanış:
- Kullanıcı kabulü/eğitim:

## Riskler ve kararlar

| Tarih | Risk/karar | Etki | Karar/sahip |
|---|---|---|---|

## İlerleme günlüğü

Her çalışma sonunda ne yapıldığı, hangi testlerin çalıştığı ve sıradaki kesin adım eklenir.

## Tamamlanma kanıtı

- [ ] Kabul kriterleri.
- [ ] Test komutları ve sonuçları.
- [ ] Migration kanıtı.
- [ ] Güvenlik ve tenant negatif testleri.
- [ ] Doküman/OpenAPI güncellemesi.
- [ ] Rollback veya compensation denemesi.
- [ ] Kaynak→alt defter→GL ve rapor mutabakat kanıtı.
- [ ] Süreç sahibi, eğitim/pilot ve operasyon exception runbook’u.
- [ ] Açık soruların sahipleri.
- [ ] Master fazı ve kapısına etkisi değerlendirilip gerekiyorsa MASTER_PLAN.md güncellendi.
