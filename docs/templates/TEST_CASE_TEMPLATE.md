# TEST-XXXX — Senaryo Başlığı

- Gereksinim: `MOD-INV-000`
- Seviye: unit | property | db-integration | contract | E2E | security | performance | restore | UAT
- Otomasyon: yes/no ve gerekçe
- Sahip:

## Amaç ve risk

Bu testin yakaladığı hata ve iş etkisi.

- Süreç döngüsü:
- İç kontrol:
- Kaynak→alt defter→GL/rapor invariantı:

## Ön koşullar

Ortam, build/migration, synthetic fixture, kullanıcı/rol/scope, feature flag ve dış fake/sandbox.

## Veri

Şirket/şube, para/kur/tarih, belgeler ve beklenen başlangıç bakiyesi. Gerçek PII yok.

- Commitment/economic event/resource/agent:
- Document/effective/recorded/posted times:
- Rule/tax/rate/rounding snapshot:
- Open item/payment/allocation/statement:
- Expected subledger/journal/report control totals:

## Adımlar

1. ...
2. ...

## Beklenen sonuç

API/UI, DB invariant, muhasebe/stok/cari, audit, outbox/notification ve metric sonucu.

- Business/accounting/allocation/bank/integration status ayrı:
- Source lineage ve reversal/repost generation:
- Subledger = GL control account:
- Report cross-foot ve drill-down:

## Negatif ve eşzamanlılık

Yetkisiz scope, duplicate/retry, paralel istek, timeout/process kill ve ters/düzeltme.

- Distinct-person quorum/delegation bypass:
- Backdate + period/tax/inventory lock:
- Allocation limit/unallocation:
- Statement duplicate/control-total mismatch:
- Repost dış side effect ve source checksum mismatch:
- Concurrent report as-of/pagination:

## Temizlik / yeniden çalıştırma

Test idempotentliği, izole DB ve artefakt saklama.

## Kanıt

CI run, commit, ekran/rapor gerekiyorsa, korelasyon ve uzman imzası.

Golden fixture version, source/profile version, as-of/projection generation, before/after hash ve reconciliation summary eklenir.
