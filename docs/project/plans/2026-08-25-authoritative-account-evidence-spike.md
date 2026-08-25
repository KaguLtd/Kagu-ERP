# MP-03 Authoritative Account Evidence Technical Spike

- **Amaç:** Journal satırlarının hesap planı sürümü, aktiflik ve posting-kind kanıtını PostgreSQL'den fail-closed yüklemek.
- **Master fazı:** MP-03 / sıra 2 ve backlog 20.
- **Risk:** R4 — yanlış/özet/pasif hesaba mali kayıt hazırlama ve cross-company veri sızıntısı.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-006`, `API-003`.
- **Definition of Ready:** Teknik immutable evidence dilimi için geçer. KKTC hesap kodları, import kaynağı, authoring/activation workflow'u ve posted persistence kapsam dışıdır.

## Kapsam ve sınırlar

- Tenant/company scoped chart version ve account posting snapshot tabloları.
- Runtime için forced RLS ve yalnız SELECT yetkisi.
- Caller-owned transaction içinde journal'ın tüm distinct hesaplarını exact seçilen chart version'dan yükleyen adapter.
- Eksik, pasif, summary, yanlış version ve cross-company negatif gerçek DB testleri.
- Hesap kodu, isim veya resmi chart içeriği hard-code edilmez.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Migration ve privileges | Current/empty DB | completed |
| 2 | Authoritative loader | Build/integration | completed |
| 3 | Negative scope/postability | Real PostgreSQL | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- `0010` mevcut PostgreSQL'e uygulandı; ikinci koşu 0 migration ile geçti.
- Temiz PostgreSQL üzerinde 10 migration ve aynı gerçek DB entegrasyon seti geçti.
- Loader exact chart version içinden journal'ın distinct hesaplarını yükledi; eksik evidence fail-closed oldu.
- Aktif posting hesap pozitif; summary ve inactive hesap negatif testleri geçti.
- Başka company scope'unda chart görünmedi; runtime iki evidence tablosunda yalnız SELECT yetkisine sahip.
- Full verify .NET, web, current/empty PostgreSQL, Keycloak, isolated restore ve Android kapılarıyla geçti.
- Bu evidence posted journal veya resmi KKTC chart içeriği değildir.
