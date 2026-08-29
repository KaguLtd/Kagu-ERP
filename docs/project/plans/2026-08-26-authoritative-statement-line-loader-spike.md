# MP-03 Authoritative Statement-line Loader Technical Spike

- **Amaç:** Immutable normalize statement-line snapshot'ını PostgreSQL'den company scope içinde Treasury domain modeline yeniden kurmak.
- **Master fazı:** MP-03 / backlog 17 teknik ön koşulu.
- **Risk:** R4 — bozuk hash/parser/identity kanıtının kabulü veya cross-company banka verisi sızıntısı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `BNK-INV-003`, `BNK-STMT-001`.

## Sınır

Loader salt okunurdur; dosya erişimi, parser, external-key üretimi, reconciliation veya GL davranışı eklemez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Scope-bound statement-line load | Infrastructure | completed |
| 2 | Domain invariant ile yeniden doğrulama | Integration | completed |
| 3 | Missing/cross-company fail-closed | Real PostgreSQL | completed |

## Tamamlanma kanıtı

- Loader aynı caller-owned transaction ve tenant/company scope içinde bütün normalized line alanlarını yükler.
- External identity, currency ve line snapshot domain fabrikalarından yeniden geçirilir; hash/parser/amount invariantları sessizce atlanmaz.
- 125.50 GBP satır bütün immutable alanlarıyla yeniden kuruldu; başka company scope'unda aynı ID `null` döndü.
- Release derlemesi ve gerçek PostgreSQL integration kapısı geçti.

Loader raw dosyayı açmaz, external key türetmez ve reconciliation kararı üretmez; MP-03 `proposed` kalır.
