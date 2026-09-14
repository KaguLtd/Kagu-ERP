# MP-04 çıkış birim maliyeti seçimi

- Amaç: Onaylı son maliyet / geçmiş yoksa sıfır kuralını stok çıkışıyla ilişkili immutable domain sonucuna bağlamak.
- Faz: MP-04; INV-COST-001; risk R3. Sahip atanmadı. Durum validating (domain dilimi).
- Ready: DEC-MP01-011 son maliyet, yeni alışta eski çıkışı değiştirmeme ve geçmiş yoksa
  sıfır kararları kullanıcı tarafından onaylandı. Mevcut stock movement ve watermark temeli var.
- Okunan kaynaklar: AGENTS, MASTER_PLAN MP-04, docs/README yönlendirmesi, veri mimarisi,
  Inventory maliyet sözleşmesi, DEC-011 ve önceki kapanış/uygulama planları.

## Kapsam / done when

Known / NoHistory ayrı oluşturulur; null veya hata otomatik sıfır değildir. Bilinen sıfır maliyet
ile geçmiş bulunamadığı için sıfır ayrı provenance taşır. Exact decimal birim maliyeti korunur;
tenant/company/item/warehouse/UOM ve effective sequence/recorded cutoff uyuşmazlığı reddedilir.
Snapshot kimliği, generation ve checksum yeniden üretilebilirlik için korunur. Kaynak
satınalma faturalarından türetilen maliyet projection'ı olacak; bu domain nesnesi onun yerine
fatura veya DB kanıtı üretmez. Producer'ın doğru/latest history seçimi ayrıca uygulanmalıdır.

Stok miktarı yeterlilik şartı eklenmez. Hareketli ortalama yeniden hesaplama, yuvarlanmış
ledger tutarı, DB history loader/writer, GL, API, yetki grant'i ve migration kapsam dışıdır.
Bu ara sonuç posting izni değildir. Yeni bağımlılık/PII yok; persistence olmadığı için
rollback veri telafisi gerektirmez. Finansal değer farkı hesabı varsayılmaz.

## Doğrulama

Unit senaryoları: son maliyetin exact korunumu, recorded-zero/no-history-zero ayrımı,
yeni maliyetin eski sonucu değiştirmemesi, scope/UOM/cutoff, negatif maliyet, boş kimlik,
geçersiz currency ve null evidence. Runtime kullanıcı MP-sonu kadansında; dar compile ayrı.

## Sonuç — 12 Eylül

- Domain ve birim senaryoları eklendi. `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj
  -c Release --no-restore -v:q`: 0 uyarı/hata, 18.08 sn. `git diff --check` temiz.
- Unit/DB/finansal runtime testi çalıştırılmadı; kullanıcı MP-sonu kadansı korunuyor.
  Derleme davranış kanıtı değildir. Yeni SQL/endpoint olmadığından bu dilimde migration/API
  testi yok; gerçek source→cost→GL zinciri henüz tamamlanmadı.
- Watermark boş/geçersiz evidence yerine üretilemez; ilk çıkış için de producer geçerli
  pre-issue cutoff/generation kanıtını oluşturmalıdır. Bilinen maliyetin gerçekten son geçerli
  maliyet olması ve fonksiyonel currency/profile eşleşmesi producer sorumluluğunda açık kalır.
- MP kapısı değişmedi, commit/push yapılmadı.
