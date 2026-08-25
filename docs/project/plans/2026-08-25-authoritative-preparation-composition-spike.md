# MP-03 Authoritative Preparation Composition Technical Spike

- **Amaç:** Journal preparation sırasında hesap, boyut, kur ve dönem kanıtlarını caller yerine aynı PostgreSQL transaction'ında authoritative kaynaklardan yüklemek.
- **Master fazı:** MP-03 / backlog 20 öncesi güvenlik ve finansal doğruluk kapısı.
- **Risk:** R4 — caller tarafından üretilmiş veya değiştirilmiş doğrulama snapshot'ının mali hazırlık akışına kabul edilmesi.
- **Durum:** completed.
- **Sahip:** Ürün, teknik ve muhasebe sahipleri `atanmadı`.
- **Requirement ID:** `ACC-INV-006`, `ACC-INV-007`, `ACC-INV-008`, `API-003`.
- **Definition of Ready:** Authoritative read modelleri hazırdır; approval ve posted persistence kapsam dışıdır.

## Milestone'lar

| No | Dikey dilim | Kanıt | Durum |
|---:|---|---|---|
| 1 | Caller-supplied evidence'i request'ten kaldır | Build/unit | completed |
| 2 | Permission-first ve transaction-bound authoritative composition | Real PostgreSQL | completed |
| 3 | Commit/rollback/denied/closed-period regresyonları | Integration | completed |
| 4 | Full verify ve docs | Repository gate | completed |

## Tamamlanma kanıtı

- `JournalPreparationRequest` artık caller-supplied account, dimension veya currency validation nesnesi taşımıyor; yalnız immutable chart version kimliğini taşıyor.
- Orchestrator permission'ı kanıt tablolarına erişmeden önce denetliyor ve dönem, hesap, boyut ile kur kanıtlarını aynı caller-owned PostgreSQL transaction'ında authoritative loader'lardan üretiyor.
- Gerçek PostgreSQL testleri tam commit, caller rollback, yetkisiz aktör ve kapalı dönem senaryolarında geçti; yetkisiz ve kapalı yollar hiçbir journal fact üretmedi.
- `scripts/verify.ps1` 25 Ağustos 2026 tarihinde .NET, web, mevcut/boş PostgreSQL, restore, RLS, auth ve Android kapılarının tamamında geçti.

## Açık sınırlar

Bu composition kaynak belgeyi journal draft'a dönüştürmez, public endpoint açmaz, approval kanıtı tüketmez ve posted journal/GL sonucu üretmez.
