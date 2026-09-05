# Kagu ERP OpenAPI sözleşmesi

`KaguERP.Api.json`, API projesinin Release build'i sırasında OpenAPI 3.1 olarak otomatik üretilir.
Dosyayı elle düzenlemeyin; endpoint metadata'sını değiştirip aşağıdaki komutla yeniden üretin:

```powershell
dotnet build src/Erp.Api/KaguERP.Api.csproj -c Release
```

Architecture harness; Sales operation kimliklerini, bearer gereksinimini, zorunlu idempotency ve
concurrency header'larını, transition action allowlist'ini ve cevap matrisini bu dosya üzerinden
doğrular. TS/Kotlin istemcileri yalnız bu onaylı dosyadan üretilecektir.
