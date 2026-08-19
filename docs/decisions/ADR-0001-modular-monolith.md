# ADR-0001 — Modüler Monolit

- Durum: Accepted
- Tarih: 2026-08-18
- Son doğrulama: 2026-08-19 — açık kaynak ERP ve AIS literatürü karşılaştırması

## Bağlam

Orta ölçekli tek firma/grup ERP'si; finansal işlemlerde güçlü transaction, küçük ekip, tek Linux sunucu ve düşük operasyon karmaşıklığı istiyor. Mikroservisler dağıtık transaction, mesaj, gözlem ve dağıtım maliyetini erkenden artırır.

## Karar

ASP.NET Core içinde modüler monolit kurulacak. Her modül kendi Domain/Application/Infrastructure/API katmanına ve PostgreSQL şemasına sahip olacak. Modüller başka şemaya doğrudan yazmayacak; uygulama contract'ı veya event kullanacak. Tek process zorunlu değildir; API ve worker aynı kod tabanından ayrı process olabilir.

## Sonuçlar

- Finansal iç işlemler tek DB transaction'ında güçlü tutarlı olabilir.
- Tek build/deploy daha basittir; module boundary test/lint/review ile korunmalıdır.
- Tek DB arızası tüm sistemi etkiler; backup/DR kritik.
- Modül ekipleri bağımsız deploy edemez.

## Reddedilenler

- Başlangıç mikroservisleri: ölçülmüş bağımsız ölçek/release ihtiyacı yok.
- Katmansız monolit: veri ve kod sınırları zamanla bozulur.
- Tam event sourcing: sorgu, göç ve operasyon karmaşıklığı gereksiz.

## Yeniden değerlendirme

Bir modül kalıcı farklı ölçek, güvenlik/felaket alanı veya bağımsız release ihtiyacı gösterirse; contract/outbox sınırı üzerinden çıkarma ADR'si yazılır. Önce ölçüm ve ekip operasyon kapasitesi gerekir.

## v1.1 açıklaması

Modül sınırı yalnız klasör/schema değildir; her modül kendi commitment/economic event ve subledger gerçeğinin sahibidir. Accounting ortak PostingRequest contract’ıyla GL projection üretir; Reporting yalnız read model kurar. Treasury Payment, Party Allocation ve bank reconciliation doğrudan tablo yazımıyla birbirine bağlanmaz.

Odoo/ERPNext/OFBiz/iDempiere gibi olgun sistemlerde görülen bütünleşme gereksinimi mikroservis zorunluluğu değildir. Tek transaction gereken source–subledger–GL doğruluğu, küçük ekip ve tek host koşulu bu kararı güçlendirir. Ayrıştırma ancak ölçülen farklı ölçek/failure-domain ve versioned contract kanıtıyla yapılır.
