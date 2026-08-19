# Modül Adı

## 1. Amaç ve sınır

Modülün sahip olduğu gerçek, kullandığı/yayımladığı contract ve kapsam dışı.

## 2. Kullanıcılar ve yetkiler

Rol, permission, company/branch/depo scope, tutar limiti ve görevler ayrılığı.

## 3. Varlıklar

Aggregate, entity/value object, iş anahtarı ve kişisel/hassas alanlar.

### 3.1 REA ve kaynak gerçek

| Tür | Kayıt | Açıklama |
|---|---|---|
| Commitment | | |
| Economic event | | |
| Resource | | |
| Internal agent | | |
| External agent | | |

Kaynak belge/satır bağlantıları ve çoktan çoğa dönüşüm:

## 4. Durum makineleri

Durumlar, izinli geçiş, aktör, koşul, sonuç ve ters/düzeltme.

## 5. Değişmez kurallar

- `MOD-INV-001`: Sunucuda ve mümkünse DB'de uygulanan kural.

## 6. İş akışları

Normal, istisna, iptal, eşzamanlılık ve dış sistem belirsizliği.

- BPMN aktör/swimlane:
- Exception state ve çözüm komutu:
- Business reversal/correction:
- Projection repost varsa sınırı:
- Etkin/kayıt/posting tarihleri ve cut-off:

## 7. Muhasebe/vergi/stok/cari etkisi

Kaynak olay, kayıt şablonu, anlık görüntü, mutabakat ve dönem etkisi.

| Olay | Alt defter | Borç | Alacak | Vergi/stok | Rule snapshot | Reversal |
|---|---|---|---|---|---|---|
| | | | | | | |

Payment, allocation ve bank reconciliation ilişkisi:

## 8. Veri modeli

Şema, tablo, unique/FK/check/index, RLS, retention ve migration.

## 9. API ve olaylar

Endpoint, permission/scope, request/response, idempotency/ETag, outbox/inbox ve hata kodu.

## 10. Web ve Android

Ekran, loading/empty/error/conflict/offline/accessibility ve platform sınırı.

## 11. Rapor ve dışa aktarma

Tanım, kesim zamanı, para birimi, drill-down, yetki ve CSV güvenliği.

- As-of/watermark/projection generation:
- Control account ve cross-foot:
- Source-to-report drill-down:
- Export manifest/checksum:

## 12. Güvenlik/gizlilik

Tehdit, hassas veri, audit, maskeleme, saklama ve dış transfer.

### 12.1 İç kontrol kataloğu

| Risk | Kontrol | Owner | Önleyici/tespit | Sıklık | Kanıt | Exception SLA |
|---|---|---|---|---|---|---|
| | | | | | | |

## 13. Gözlemlenebilirlik

Metric/log/trace, iş alarmı ve runbook.

## 14. Test ve kabul

Unit/property, PostgreSQL integration, API/authz, concurrency, E2E, performans, restore ve uzman kabulü.

- Golden process cycle:
- Subledger–GL reconciliation:
- Backdate/lock/repost:
- Taksit/allocation/reconciliation:
- Rapor cross-foot/drill-down:
- Süreç sahibi/UAT:

## 15. Açık sorular ve sonraki faz

Karar sahibi, son tarih ve no-go etkisi.
