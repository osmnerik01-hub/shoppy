# Mini e-ticaret

ASP.NET Core / EF Core (.NET 10), AngularJS 1.8.3 ile katalog, stok ve sipariş demosu.

## Yerelde çalıştırma



Proje kökünde bağlantıları ayarlayıp uygulamayı başlatın:

```powershell
$env:ConnectionStrings__Catalog = 'Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=YOUR_LOCAL_PASSWORD;Timeout=5'
powershell -ExecutionPolicy Bypass -File ./scripts/start-local.ps1
```


Doğrulama için yerel PostgreSQL üzerinde geçici veritabanı oluşturup silebilen (`CREATEDB`) test hesabını ayarlayın:

```powershell
$env:CATALOG_TEST_POSTGRES = 'Host=localhost;Database=postgres;Username=postgres;Password=YOUR_LOCAL_PASSWORD'
./scripts/verify.ps1
# Normal başlatıcı kapalıyken ve uygulama bağlantıları ayarlanmış olarak:
./scripts/verify.ps1 -System
```


CI aynı betiği ayrılmış yerel Windows runner üzerinde çalıştırır; hazırlık [yerel kılavuzda](docs/local-windows.md). Raporlar, ekran görüntüleri ve servis logları `artifacts` altında, CI'da `verification-results` artifact'ında saklanır. Paylaşmadan önce loglardaki bağlantı ayrıntılarını inceleyin.

## Yapılandırma ve demo sınırları


Kimlik doğrulama, ödeme, kargo ve üretim operasyonları kapsam dışıdır. Sepetteki her ürün ayrı sipariştir; sepet bütünü için atomiklik yoktur. SQLite kalıcı dosyası tek host demosudur; bağımsız hostlarda yatay ölçekleme için paylaşımlı sunucu veritabanı ve migration stratejisi gerekir. Inventory/Orders EnsureCreated şeması otomatik şema yükseltmesi sağlamaz. AngularJS kullanım ömrünü tamamlamıştır. Inbox/outbox temizliği, zehirli mesaj yönetimi ve üretim sır yönetimi ayrıca ele alınmalıdır.

Ayrıntılar: [mimari](docs/architecture.md), [Catalog](docs/catalog.md), [Inventory](docs/inventory.md), [Orders](docs/orders.md), [Gateway](docs/gateway.md), [Web](docs/web.md).


Current HTTP delivery configuration: .NET 10 SDK (global.json), Node.js 24/npm and PostgreSQL 17+ are required. SQLite needs no service. Set ConnectionStrings__Catalog to your PostgreSQL connection; retain schema migration permissions. Run scripts/start-local.ps1 or scripts/start-existing-local.cmd for the existing data profile. SDK/Node, PostgreSQL reachability, occupied ports and existing SQLite file checks remain enabled.

Orders uses Inventory__Url (default http://localhost:5102); Inventory uses Orders__Url (default http://localhost:5103). The launcher supplies the selected ports. Workers POST persisted Outbox payloads to /internal/messages with a 5 second timeout and retry every second. Sent is written only after successful consumer completion. Inbox and persistent order decisions prevent a second stock effect, including lost HTTP responses and process restarts. Unsupported event versions receive 400 and remain pending for operator correction. Internal endpoints must stay on the trusted service network; Gateway does not publish them.

HTTP teslimatı için .NET 10 SDK (global.json), Node.js 24/npm ve PostgreSQL 17+ gerekir. SQLite ayrı servis gerektirmez. `ConnectionStrings__Catalog` bağlantısını ve migration yetkilerini ayarlayın. `scripts/start-local.ps1` veya mevcut veriler için `scripts/start-existing-local.cmd` çalıştırılır. SDK/Node, PostgreSQL, port ve mevcut SQLite dosyası kontrolleri korunur. Varsayılan arayüz http://localhost:5100 adresindedir; Ctrl+C uygulama süreçlerini kapatır.

Orders, `Inventory__Url` (varsayılan http://localhost:5102); Inventory, `Orders__Url` (varsayılan http://localhost:5103) kullanır. Başlatıcı seçilen portları aktarır. Worker kalıcı Outbox içeriğini `/internal/messages` ucuna POST eder; HTTP zaman aşımı 5 saniye, yeniden deneme aralığı 1 saniyedir. Sent yalnızca tüketici işlemi tamamlanıp başarılı yanıt alındıktan sonra yazılır. Inbox ve kalıcı sipariş kararları tekrar teslimatta, yanıt kaybında ve süreç yeniden başladığında ikinci stok etkisini engeller. Desteklenmeyen olay/sürüm 400 alır ve operatör sözleşmeyi düzeltene kadar bekler. İç uçlar güvenilir servis ağında tutulmalıdır; Gateway bunları yayınlamaz.

RabbitMQ artık gerekli değildir. Servisine ve eski kuyruklarına dokunulmaz. Yalnızca eski broker kuyruğunda kalan mesajlar otomatik taşınmaz. Sent=false Outbox kayıtları HTTP ile gönderilir; Sent=true kayıtlar yeniden açılmaz. SQLite ve PostgreSQL verileri korunur. `scripts/control-local.ps1` inventory/orders yeniden başlatma ve shutdown işlemlerini destekler. CI yalnızca `CATALOG_TEST_POSTGRES` ve `CATALOG_CONNECTION_STRING` bağlantı sırlarını gerektirir; broker CLI veya URI/vhost ayarı gerekmez. Sistem kontrolleri Inventory kesintisini ve Orders yeniden başlatmasını kapsar.
