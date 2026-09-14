# Sevk yuvarlama politikasının authoritative kaynağa bağlanması

- MP-04, SALES-DSP-009 / INV-COST-006, risk R3; durum validating.
- Amaç: Caller'ın verdiği scale yerine Accounting-owned immutable policy ID/version kaydını kullanmak.
- Ready: MP-03 authoritative currency evidence tamamlanmış, 0012 immutable policy tablosu mevcut; DEC-MP01-006 iki basamak AwayFromZero kararı var.
- Okunanlar: Accounting AGENTS, GL modülü, veri mimarisi, ortak iş akışları, MP-03 authoritative currency planı, Inventory/Sales ve son dispatch-position planı.
- Sahip: Teknik Codex, ürün/muhasebe atanmadı; tarih 13 Eylül 2026.

## Kapsam ve done when

Accounting iç yayımlanmış loader company-scoped policy ID/version okur; Inventory Accounting tablosuna erişmez. Bootstrap cost-view + scope/audit kontrollerinden sonra loader sonucunun iki basamak AwayFromZero olduğunu doğrular ve allocated dispatch preview'a geçirir. Dönen sonuç tam policy snapshot'ını taşır; sahte/eksik/eski policy fallback ile tamamlanmaz. Authoring, şirketin aktif policy seçimi, functional currency profile ve effective-date policy ataması bu kayıt modelinde mevcut değildir; bu dilim seçilmiş immutable kaydı doğrular, aktif politikayı seçtiği iddia edilmez.

Yeni migration/HTTP/grant yok. Kaynak→stock→GL posting yok. Yanlış actor/scope izni loader çağrısından önce kesilir; iş tutarları loglanmaz. Caller transaction ve outer savepoint kısmi audit'i geri alır. Geri dönüş yeni çağrıyı kaldırmaktır. Missing/version/mode/scale/permission negatifleri ve DB source→amount senaryosu yazılır. Derleme runtime/finansal güvenlik testi yerine geçmez; MP-04 toplu koşu kanıtı beklenir. Commit/push yok.

## 13 Eylül sonuç

Accounting-owned reader ve Bootstrap authority composition eklendi; DB fixture içinde version 7 policy→37,04 TRY satır tutarı, eski sürüm, eksik policy, başka şirket, yanlış mode/scale, missing permission ve actor uyuşmazlığı/audit korunumu senaryoları hazırlandı. Policy fixture sentetiktir; production policy ataması değildir. `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release --no-restore -v:q`: 30,50 sn, sıfır uyarı/hata; Accounting ve Bootstrap dahil derlendi. Runtime/DB testleri MP-04 toplu kadansında bekler; özellikle RLS/concurrency/golden mali kanıt yeni başarılı sonuç olarak sunulmaz. Mevcut CodeIntegrity engeli bypass edilmedi. Yeni migration veya API sözleşmesi açılmadı; commit/push yapılmadı.
