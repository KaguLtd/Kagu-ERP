# Çok satırlı sevk maliyet toplamı mutabakatı

- MP-04 / INV-COST-009; R3; validating; 13 Eylül 2026.
- Amaç: Satır tutarları ve ürün/depo miktarlarıyla belge toplamını tek immutable sonuçta uzlaştırmak.
- Ready: INV-COST-001/006 exact selection/amount ve Bootstrap batch preview mevcut; yeni fiyat/vergi/negatif maliyet politikası seçilmiyor.
- Okunanlar: MP-04 master, AGENTS, docs/README, Inventory maliyet, valuation planı ve mevcut domain/preview/testler.
- Sahip: Teknik Codex; ürün/muhasebe atanmadı.

## Kapsam ve done when

1–500 satır aynı tenant/company/source type/event/version/purpose, currency, rounding policy/scale taşımalı. Movement/source line/stock position tekrarları reddedilir. Aynı stok pozisyonunda UOM aynı olmalı; her satırın maliyet kesiti ve origin'i ayrı korunur, farklı kesitler tek maliyete indirgenmez. Amount public record alanlarına güvenilmez, exact hesapla tekrar karşılaştırılır. Toplam tutar numeric(20,4), pozisyon bazlı signed miktar numeric(20,6) sınırlarında kalır. Kaynak satır sırası ve snapshot'lar korunur.

Bootstrap LoadAmountsAsync toplam doğrulamayı audit tamamlanmadan yapar; hata bütün önceki selection audit'lerini rollback eder. Yeni GL posting/API/migration/grant yok. Runtime MP-04 toplu testte; compile mali/DB güvenlik kanıtı değildir. Commit/push yok.

## İlerleme

### Devam: canonical hesap snapshot özeti

INV-COST-009 R3 devamında batch sonucuna sürümlü SHA-256 fingerprint eklenir. Exact snapshot'ın kaynak, hareket, effective/recorded tarih, policy/scale, history watermark/generation/cutoff/checksum ve maliyet origin/amount alanları kapsamda. Satırlar movement ID ile canonical sıralanır; decimal değerler invariant G29 yazılır. Input sırası, kültür ve trailing zero farkı özeti değiştirmez. Yeni hareket ID/kayıt zamanı değiştirir: bu kullanıcı request idempotency hash'i değil, hazırlanmış tam sonucun özetidir. DB/source provenance, yetki veya dijital imza yerine geçmez. Done when: alan kapsamı, canonical/difference testleri, derleme ve doküman; persistence/grant/API yok.

Uygulandı: immutable batch Fingerprint alanı; sıra/tr-TR kültür/amount ve unit-cost trailing zero eşdeğerlikleri, source snapshot/policy/generation-checksum değişimi senaryoları yazıldı. `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj -c Release --no-restore -v:q`: 11,66 sn, sıfır uyarı/hata. `git diff --check` içerik hatası yok; eski lock dosyalarında CRLF/LF uyarıları var. Runtime MP-04 kadansında bekler; bu oturumda integration yeniden derlenmedi (public imza/SQL değişmedi). Commit/push yok.

Derleme kanıtı: `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj -c Release --no-restore -v:q` 16,75 sn; `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release --no-restore -v:q` 32,50 sn. İkisi de sıfır uyarı/hata. `git diff --check` içerik hatası yok; mevcut lock dosyalarında CRLF/LF uyarıları var. Commit/push yapılmadı.

Immutable batch toplamı ve mevcut Bootstrap amount pipeline bağlantısı eklendi. Unit senaryoları 0,125 × 3 satırının 0,38 sonucu üzerinden 0,76 belge toplamı; input copy; amount tamper; duplicate source/movement/position; farklı currency/policy/source; miktar/tutar toplam taşması; ayrı stok pozisyonları ve 500/501 sınırını kapsar. DB harness'e tek başına geçerli iki satırın toplam miktarı numeric sınırını aştığında tüm selection audit'lerinin rollback kontrolü eklendi. Runtime MP-04 kapanış paketinde bekler; compile gerçek DB/finansal kanıt değildir. Üretim invoice producer ve stok/GL zinciri hâlâ açık, MP kapısı değişmedi.
