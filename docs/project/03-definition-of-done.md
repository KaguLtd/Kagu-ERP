# Definition of Done

Bir iş yalnız kod yazıldığı veya ekranda çalıştığı için tamamlanmış sayılmaz. Aşağıdaki maddelerin ilgili olanları kanıtlanmalı; uygulanmayan madde gerekçelendirilmelidir.

## 1. Gereksinim ve ürün

- [ ] Gereksinim/issue kimliği, kullanıcı sonucu ve kabul senaryosu açık.
- [ ] Master fazı, risk sınıfı, ilerletilen kapı ve Definition of Ready sonucu kayıtlı.
- [ ] Kapsam dışı ve varsayımlar yazılı.
- [ ] Domain terimleri sözlükle uyumlu.
- [ ] İlgili rol/şirket/şube/depo kapsamı tanımlı.
- [ ] Loading/empty/error/conflict/forbidden/cancel durumları düşünülmüş.
- [ ] Ürün sahibi/iş temsilcisi kabul kanıtı var.

## 2. Domain ve mali doğruluk

- [ ] Invariant ve state transition sunucuda uygulanıyor.
- [ ] Kesinleşmiş mali/stok kayıtları silinmiyor; ters/düzeltme yolu var.
- [ ] Para decimal, para birimi/kur/yuvarlama açık.
- [ ] Kaynak belge→alt defter→muhasebe izi var.
- [ ] Idempotency ve eşzamanlılık davranışı tanımlı/testli.
- [ ] Alt defter ve rapor mutabakatı beklenen sonucu veriyor.
- [ ] Yürürlük tarihli kural eski işlemi değiştirmiyor.

## 3. Veri tabanı

- [ ] Migration gözden geçirilmiş, staging temsili hacimde testli.
- [ ] PK/FK/unique/check/index ve null davranışı doğru.
- [ ] `company_id`/scope ve RLS politikası var; negatif testli.
- [ ] Runtime rolü owner/superuser değil.
- [ ] Transaction/outbox atomikliği kanıtlı.
- [ ] Büyük tablo lock/backfill/rollback veya roll-forward planı var.
- [ ] Backup/restore etkisi değerlendirildi.

## 4. API

- [ ] Route/verb/status ve Problem Details standardına uygun.
- [ ] Authentication + permission + scope policy uygulanıyor.
- [ ] OpenAPI güncel; generated client uyumlu.
- [ ] Input/output sınırları, filtre allowlist ve sayfalama var.
- [ ] ETag/Idempotency gereken komutta uygulanıyor.
- [ ] PII/secret hata veya loga sızmıyor.
- [ ] Backward compatibility/deprecation değerlendirildi.

## 5. Web

- [ ] Tasarım sistemi bileşen/tokenı kullanılmış; ham rastgele stil yok.
- [ ] Klavye, odak ve WCAG 2.2 AA kontrolleri.
- [ ] Responsive ve veri yoğun tablo/form durumları.
- [ ] Token browser storage'da değil; CSRF/XSS kontrolü.
- [ ] API/cache şirket bağlamına göre izole.
- [ ] Bileşen + kritik Playwright testi.
- [ ] Bundle/latency bütçesinde regresyon yok.

## 6. Android

- [ ] Sistem browser + PKCE; Keystore ve logout temizliği.
- [ ] Room cache kullanıcı/şirket izolasyonu ve TTL.
- [ ] Offline/sync/idempotency/conflict davranışı testli.
- [ ] Telefon/tablet, font scaling ve TalkBack semantics.
- [ ] Backup/log/deep link/hassas dosya güvenliği.
- [ ] Min/target cihazlarda test ve dağıtım uyumluluğu.

## 7. Güvenlik ve gizlilik

- [ ] Tehdit modeli ve ASVS kontrol etkisi gözden geçirildi.
- [ ] Least privilege ve görevler ayrılığı.
- [ ] Injection/XSS/CSRF/SSRF/file upload ilgili testleri.
- [ ] Secret yalnız güvenli store/config yolunda.
- [ ] PII amaç, erişim, saklama ve dış transfer değerlendirmesi.
- [ ] Audit olayı yeterli ama hassas veri içermiyor.
- [ ] SAST/SCA/secret/image taramaları kabul sınırında.

## 8. Test

- [ ] Olumlu, olumsuz, sınır, rol ve şirket izolasyon testleri.
- [ ] Gerçek PostgreSQL integration gereken yerde kullanılmış.
- [ ] Concurrency/retry/duplicate/failure injection senaryosu.
- [ ] Üretim hatasıysa regresyon testi önce/beraber eklendi.
- [ ] Test verisi sentetik ve deterministik.
- [ ] Flaky/skip test yok veya tarihli risk kabulü var.

## 9. Gözlemlenebilirlik

- [ ] Structured log/trace/metric ve korelasyon.
- [ ] İş başarısı/başarısızlığı ve queue/outbox görünür.
- [ ] PII/secret label/log değil.
- [ ] Yeni kritik failure için alarm/runbook.
- [ ] Health/readiness doğru ve bağımlılık kesintisinde yanıltmıyor.

## 10. Operasyon ve release

- [ ] Config/secrets/environment değişikliği belgeli.
- [ ] Container non-root, healthcheck ve resource davranışı.
- [ ] Deploy/rollback/roll-forward ve migration sırası.
- [ ] Yedek/restore/saklama etkisi.
- [ ] Feature flag sahibi ve kaldırma tarihi.
- [ ] Release notes, SBOM/digest ve destekli istemci sürümü.
- [ ] Staging smoke ve üretim sonrası doğrulama adımı.

## 11. Dokümantasyon

- [ ] Kod/şartname/README ve OpenAPI uyumlu.
- [ ] Karmaşık işin görev planı güncel; faz kapısı değiştiyse MASTER_PLAN.md kanıtla güncellenmiş.
- [ ] Mimari karar değiştiyse ADR.
- [ ] Kullanıcı/operasyon davranışı değiştiyse kılavuz/runbook.
- [ ] Hukuki kaynak veya açık soru güncellendi.
- [ ] Plan karar/gelişim/sonuç bölümleri tamamlandı.

## 12. Release düzeyi ek kapılar

- [ ] Golden mali veri tam mutabakat.
- [ ] Kritik E2E, performans ve güvenlik paketi yeşil.
- [ ] Restore tatbikatı hedef süre içinde güncel.
- [ ] Veri migration provası ve kabulü.
- [ ] KKTC resmi/mali/hukuki onaylar veya açıkça sınırlandırılmış mod.
- [ ] On-call, incident ve iş sürekliliği hazır.
- [ ] Go/no-go sorumluları imzaladı.

## 13. v1.1 muhasebe ve süreç ek kapıları

- [ ] Commitment, economic event, resource ve agent tanımlı.
- [ ] Happy path, exception, reversal/correction ve telafi akışı belgeli.
- [ ] Kaynak event → subledger → GL ve varsa allocation/reconciliation lineage tam.
- [ ] Effective/document/recorded/posted ve banka value/booking tarih semantiği testli.
- [ ] Posting rule, tax, currency, rounding ve dimension snapshot yeniden üretilebilir.
- [ ] Alt defter ile GL control account aynı as-of/scope/currency’de sıfır fark.
- [ ] Payment, allocation ve bank settlement ayrı state ve API command.
- [ ] Backdate impact, lock scope ve cut-off davranışı testli.
- [ ] Business reversal ile projection repost ayrımı korunmuş; repost dış side effect üretmiyor.
- [ ] Rapor total/cross-foot, as-of/generation ve source drill-down doğrulanmış.
- [ ] Distinct-person approval/quorum, field/record/action authorization ve negative tests geçmiş.
- [ ] İç kontrolün risk, owner, sıklık, kanıt ve exception tanımı tamam.

## 14. Release benimseme kapıları

- [ ] Süreç/kontrol sahipleri ve super-user destek rotası atanmış.
- [ ] Role göre eğitim ve yüksek riskli görev yetkinlik testi tamam.
- [ ] İki paralel kapanış veya risk sahibince onaylı eşdeğer prova sıfır/material açıklanmış farkla tamam.
- [ ] Manual continuity ve geri giriş prosedürü prova edilmiş.
- [ ] 30/60/90 günlük kullanım, kapanış, kalite ve fayda ölçüm planı var.

Kod/test tamam olsa da bu kapılar eksikse özellik üretim-ready sayılmaz; teknik olarak tamamlandı/pilot bekliyor şeklinde işaretlenir.
