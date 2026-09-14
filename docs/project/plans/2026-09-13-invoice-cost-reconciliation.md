# Fatura maliyet kaynağı mutabakatı

- MP-04 / PUR-COST-002 / INV-COST-005, R3, validating; 13 Eylül 2026.
- Amaç: Kaynak sözleşmesi yayımlanmadan fatura satırı miktar/değer toplamını ve kabul satırı kapasitesini exact doğrulamak.
- Okunanlar: master MP-04, Inventory/Purchasing sözleşmeleri, mevcut source adapter/testleri, immutable kaynak ve test kapısı kuralları.
- Ready: Allocation lineage ve decimal sınırları mevcut; yeni fiyat/vergi/maliyet yöntemi seçilmiyor.
- Sahip: Teknik Codex; ürün/muhasebe atanmadı.

## Kapsam/done when

Kaynak invoice-line bütçesi ve receipt-line kullanılabilir miktarı aynı invoice query/cutoff bağlamında taşınır. Invoice-line allocation toplamı bütçeye miktar ve eligible cost olarak exact eşit; receipt-line toplamı mevcut kapasiteyi aşamaz. Kapasite mevcut faturanın kendi allocation'ı hariç diğer kullanımlardan sonra kalan miktardır. Source factory bu değerleri gerçek authoritative kayıtlardan, aynı transaction'da kilitli okur; allocation'lardan türetmek production'da geçersizdir. Sonuç immutable wrapper ile source interface'in zorunlu dönüşüdür. Adapter mutabakat yapılmamış snapshot kabul etmez.

Bu contract SQL producer/fatura lifecycle/matching/GL değildir; sentetik fixture gerçek mali veri sayılmaz. Eksik/duplicate/extra source, yanlış scope/item/UOM/depo, fazla miktar/değer ve mutable input negatifleri hazırlanır. Yeni migration/API/grant yok; runtime MP-04 paketinde bekler, compile kanıtı yeterli kabul sayılmaz. Commit/push yok; eski interface iç kullanımdır, tüm fixture çağrıları birlikte güncellenir.

## İlerleme

`dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release --no-restore -v:q`: 32,46 sn, sıfır uyarı/hata (Purchasing contract, Inventory ve Bootstrap dahil). `git diff --check`: içerik hatası yok; mevcut lock dosyalarında CRLF/LF uyarıları var.

ReconciledSupplierInvoiceCost private-constructor wrapper, immutable bütçe/kapasite kopyaları ve exact miktar/değer/capacity kontrolleri eklendi. ISupplierInvoiceCostSource dönüşü ve Inventory adapter güncellendi; tüm mevcut provider kullanımları tarandı, mevcut tek fixture uyumlandı. Fixture'ın bütçe türetmesi açıkça synthetic olarak işaretlendi; bağımsız gerçek DB producer değildir.

Test senaryoları: exact split receipt (5 + 3, 0,625 + 0,375), düşük/yüksek fatura miktarı/değeri, yanlış UOM/item/depo/receipt, negatif/fazla hassas kapasite, değişmiş query version, duplicate/eksik bütçe, mutable input copy, iki invoice-line'ın aynı receipt kapasitesini aşması. Runtime/gerçek DB testleri MP-04 kadansına ertelendi; derleme finansal güvenlik kapısı sayılmaz. Kaynak consumption/concurrency, gerçek invoice producer ve stock/GL hâlâ açık. Yeni production authority/grant/API yok, commit/push yok.
