# ADR-0005 — İlk Üretim Tek Linux Host ve Docker Compose

- Durum: Accepted
- Tarih: 2026-08-18
- Son doğrulama: 2026-08-19 — restore ve projection-rebuild analiziyle

## Bağlam

Orta ölçekli firma kendi Linux sunucusuna kurulum istiyor. Başlangıç hacmi tek güçlü hostta karşılanabilir; küçük ekip Kubernetes/HA platformunu güvenle işletmeyebilir.

## Karar

Üretim başlangıçta harden edilmiş Ubuntu LTS hostta Docker Compose. Caddy public edge; API/web/worker/Keycloak/DB/telemetry private networks. Uzak şifreli/immutable yedek ve belgeli temiz-host restore temel telafidir.

## Sonuçlar

- Basit dağıtım, düşük altyapı maliyeti ve kolay yerel veri kontrolü.
- Host arızası tüm hizmeti keser; RTO restore hızına bağlıdır.
- Compose/image/config/version ve volume disiplini zorunlu.
- “Zero downtime” garanti edilmez; bakım penceresi gerekir.

## Reddedilenler

- Tek container/process: izolasyon ve bakım zayıf.
- Kubernetes: ekip/node/HA iş gerekçesi yok.
- DB'yi public host portuna açmak: yasak.

## Yeniden değerlendirme

SLO/RTO, kapasite, bakım kesintisi veya felaket alanı tek hostla karşılanmazsa ikinci host, load balancer, PostgreSQL HA/managed DB ve object storage seçenekleri ölçümle ADR'ye alınır.

## v1.1 açıklaması

Tek host kararı restore sorumluluğunu artırır. Canonical DB, legal archive/raw integration objects, identity/config ve uzak immutable backup aynı failure domain’de kalamaz. Read model/cache yeniden üretilebilir; canonical source kaybı projection rebuild ile telafi edilemez.

Deploy drain, migration, financial smoke ve subledger/GL/report reconciliation geçmeden write trafiği açılmaz. PITR dış sağlayıcı gerçeğini geri alamayacağı için e-Fatura/banka outbox-inbox reconciliation restore runbook’unun zorunlu parçasıdır.
