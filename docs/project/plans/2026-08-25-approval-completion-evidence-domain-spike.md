# MP-03 Approval Completion Evidence Domain Spike

- **Amaç:** Posted journal öncesi sürüm bağlı, distinct-person quorum ve maker-checker kanıtını parametrik modellemek.
- **Master fazı:** MP-03 / approval ve posted persistence ön koşulu.
- **Risk:** R4 — eski belge sürümü onayı, aynı kişinin çoklu oy sayılması veya hazırlayanın kendi kaydını onaylaması.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `WFL-INV-002`, `WFL-INV-003`, `WFL-INV-005`, quorum/SoD.
- **Definition of Ready:** Parametrik invariant için geçer; required quorum ve eligible approver politikası kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Immutable approval decision/evidence modeli | Build | completed |
| 2 | Subject version ve scope sınırları | Unit | completed |
| 3 | Distinct quorum ve maker-checker negatifleri | Unit | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Ara kanıt

- Solution build 0 hata/0 uyarıyla geçti.
- Domain harness 58 kontrolle geçti; yeni kontroller exact subject version, maker-checker, duplicate approver ve yetersiz quorum negatiflerini kapsıyor.
- Architecture/API contract, web lint/typecheck/6 test/build, mevcut ve 12 migration uygulanan boş PostgreSQL, RLS, Keycloak auth, izole restore ve Android lint/unit/instrumentation build kapıları geçti.
- `scripts/verify.ps1` 25 Ağustos 2026 tarihinde eksiksiz ve başarılı tamamlandı.

## Ortam uyumluluğu

Windows Application Control, ayrı üretilen imzasız `KaguERP.Bootstrap.dll` yüklemesini `0x800711C7` ile engelledi. Güvenlik politikası değiştirilmedi veya baypas edilmedi. Composition-root kaynakları API ve Worker host derlemelerine dahil edildi; DB integration harness da gereken kaynakları kendi test assembly'sinde derliyor. Gerçek API + Keycloak auth ve tam repository doğrulaması bu düzenle geçti. Bu geliştirme ortamı uyumluluğu release artifact code-signing gereksiniminin yerine geçmez.

## Açık sınırlar

- Model required quorum değerini veya eligible approver setini seçmez; bunlar onaylı ve sürümlü workflow policy'sinden gelmelidir.
- Bu dilim approval persistence, delegation, karar iptali/reset akışı veya posted journal üretmez.
- Posting orchestration, authoritative approval instance'ını aynı tenant/company/subject/version için yükleyip bu kanıtı doğrulamadan posted sonuç üretemez.
