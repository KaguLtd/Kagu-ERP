# MP-04 çok satırlı çıkış maliyeti önizlemesi

- Amaç: Kalıcı cost publication okuması → çıkış maliyet seçimi → audit bağlantısını tek caller transaction'da kurmak.
- Faz MP-04; INV-COST-003; R3. Sahip atanmadı; durum validating (runtime kabulü bekliyor).
- Ready: INV-COST-001 domain ve INV-COST-002 immutable publication reader hazır; DEC-011
  known/no-history maliyet politikası kullanıcı onaylı. Runtime kapısı henüz geçilmedi.
- Okunan kaynaklar: MASTER_PLAN MP-04, AGENTS, docs/README, Inventory maliyet sözleşmesi,
  maliyet seçimi/yayın planları, veri mimarisi ve mevcut audit participant kodu.

## Üç iş / done when

1. Domain scope/cutoff denetimini okuma öncesinde tekrar kullanılabilir hale getir.
2. Bootstrap tek kaynak/şirketten 1–500 unique hareket/satırın maliyetini kalıcı publication'dan
   okusun; permission ve audit scope zorunlu olsun. Başarı kodları known/zero'yu ayırsın,
   audit içine maliyet tutarı yazılmasın. Herhangi bir hata bütün batch audit'ini geri alsın.
3. PostgreSQL senaryoları known+zero, ikinci satır unavailable, yanlış audit, duplicate ve
   audit SQL hatası için hazırlansın; read sonucu stok hareketi veya GL üretmesin.

## Sınırlar

Preview sadece maliyet seçer; publish, invoice, consume, stok/GL posting, dönem izni veya
on-hand yeterliliği üretmez. Endpoint, grant, migration yok. Caller commit ve dış hata/denial
audit'inin sahibidir. Bu teknik önizleme Inventory tablo sahibi okumasını kullanır; Sales
veya Purchasing tablolarına doğrudan erişmez. Authoritative invoice producer hâlâ eksiktir.
Runtime testler kullanıcı MP-sonu kadansında; dar compile ayrı kanıt. Veri telafisi savepoint'tir.

## Sonuç / doğrulama

- Domain compatibility kontrolü ayrıldı; okuma öncesinde aynı denetim çalışıyor.
- Batch source/scope, unique hareket/satır ve 500 üst sınırı; kalıcı history okuması,
  immutable maliyet seçimi ve tutarsızlık/audit hatasında tam savepoint rollback eklendi.
- DB senaryoları known+zero iki satır, sıra ve origin, ikinci satır unavailable sonrası ilk
  audit'in silinmesi, yanlış actor, duplicate, uzun trace ile audit SQL hatası ve stock movement
  üretilmemesi için hazırlandı. Testler gerçek PostgreSQL harness'ine bağlı; çalıştırılmadı.
- `dotnet build tests/Integration/KaguERP.DatabaseIntegrationChecks.csproj -c Release
  --no-restore -v:q`: 0 uyarı/hata, 8.95 sn.
- `dotnet build tests/Unit/KaguERP.DomainUnitChecks.csproj -c Release --no-restore -v:q`:
  0 uyarı/hata, 5.14 sn. `git diff --check` temiz.
- Kullanıcı MP-sonu kadansı gereği runtime/DB/finansal ve güvenlik negatifleri çalıştırılmadı.
  Derleme transaction/rollback/RLS kanıtı değildir. Üretim kod bütünlüğü ayarına müdahale yok.
- Yeni migration, permission ataması, endpoint, DB çalıştırması veya commit/push yok.
  Satınalma faturası publisher ve gerçek sevk/GL writer hâlâ eksik; MP kapısı değişmedi.
