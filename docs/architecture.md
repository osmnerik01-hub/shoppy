# Mimari temel


## Sahiplik ve veri sınırları

| Servis | Sahip olduğu veri ve karar | Yerel port | Ayrılmış veritabanı adı |
| --- | --- | --- | --- |
| Catalog | Ürün kimliği, açıklaması, satış fiyatı | 5101 | PostgreSQL catalog |
| Inventory | Ürün kimliğine göre kullanılabilir stok ve rezervasyonlar | 5102 | inventory.db |
| Orders | Sipariş, kalemler, sipariş anındaki fiyat ve durum | 5103 | orders.db |

Her servis kendi Domain, Application, Infrastructure ve Api projelerine sahiptir. Domain iş kurallarını, Application kullanım durumlarını ve portları, Infrastructure EF Core bağdaştırıcılarını, Api HTTP girişini ve bağımlılık bileşimini barındırır. Application yalnızca Domain'e; Infrastructure Application/Domain'e; Api Application/Infrastructure'a başvurabilir. Domain EF Core, ASP.NET Core ve HTTP istemcisi bağımlılığı taşımaz.

Servisler başka servisin projesini, DbContext'ini, tablolarını, bağlantı bilgisini veya migration'larını paylaşamaz. Ürün kimliği diğer servislerde yalnızca bir dış kimliktir; servisler arası foreign key ve join yoktur. İleride iletişim sürümlenmiş HTTP sözleşmeleri veya olaylarla yapılır; ortak domain modeli paylaşılmaz. Her servis kendi veritabanı kimlik bilgilerini ve migration yaşam döngüsünü yönetir. Üretimde veritabanı erişim izinleri de bu ayrımı uygulamalıdır; kaynak mimari testleri çalışma zamanı SQL erişim yetkilerinin yerine geçmez.


## Yerel çalışma


`./scripts/verify.ps1` derleme, .NET ve Web testlerini; `-System` gerçek yerel sistem ve kesinti kontrollerini çalıştırır. Architecture.Tests katman/veri sınırlarını denetler. İsteğe bağlı iz aktarımı [Gateway](gateway.md) belgesindedir.

Current HTTP delivery configuration: .NET 10 SDK (global.json), Node.js 24/npm and PostgreSQL 17+ are required. SQLite needs no service. Set ConnectionStrings__Catalog to your PostgreSQL connection; retain schema migration permissions. Run scripts/start-local.ps1 or scripts/start-existing-local.cmd for the existing data profile. SDK/Node, PostgreSQL reachability, occupied ports and existing SQLite file checks remain enabled.

Orders uses Inventory__Url (default http://localhost:5102); Inventory uses Orders__Url (default http://localhost:5103). The launcher supplies the selected ports. Workers POST persisted Outbox payloads to /internal/messages with a 5 second timeout and retry every second. Sent is written only after successful consumer completion. Inbox and persistent order decisions prevent a second stock effect, including lost HTTP responses and process restarts. Unsupported event versions receive 400 and remain pending for operator correction. Internal endpoints must stay on the trusted service network; Gateway does not publish them.

HTTP teslimatı için .NET 10 SDK (global.json), Node.js 24/npm ve PostgreSQL 17+ gerekir. SQLite ayrı servis gerektirmez. `ConnectionStrings__Catalog` bağlantısını ve migration yetkilerini ayarlayın. `scripts/start-local.ps1` veya mevcut veriler için `scripts/start-existing-local.cmd` çalıştırılır. SDK/Node, PostgreSQL, port ve mevcut SQLite dosyası kontrolleri korunur. Varsayılan arayüz http://localhost:5100 adresindedir; Ctrl+C uygulama süreçlerini kapatır.

Orders, `Inventory__Url` (varsayılan http://localhost:5102); Inventory, `Orders__Url` (varsayılan http://localhost:5103) kullanır. Başlatıcı seçilen portları aktarır. Worker kalıcı Outbox içeriğini `/internal/messages` ucuna POST eder; HTTP zaman aşımı 5 saniye, yeniden deneme aralığı 1 saniyedir. Sent yalnızca tüketici işlemi tamamlanıp başarılı yanıt alındıktan sonra yazılır. Inbox ve kalıcı sipariş kararları tekrar teslimatta, yanıt kaybında ve süreç yeniden başladığında ikinci stok etkisini engeller. Desteklenmeyen olay/sürüm 400 alır ve operatör sözleşmeyi düzeltene kadar bekler. İç uçlar güvenilir servis ağında tutulmalıdır; Gateway bunları yayınlamaz.

RabbitMQ artık gerekli değildir. Servisine ve eski kuyruklarına dokunulmaz. Yalnızca eski broker kuyruğunda kalan mesajlar otomatik taşınmaz. Sent=false Outbox kayıtları HTTP ile gönderilir; Sent=true kayıtlar yeniden açılmaz. SQLite ve PostgreSQL verileri korunur. `scripts/control-local.ps1` inventory/orders yeniden başlatma ve shutdown işlemlerini destekler. CI yalnızca `CATALOG_TEST_POSTGRES` ve `CATALOG_CONNECTION_STRING` bağlantı sırlarını gerektirir; broker CLI veya URI/vhost ayarı gerekmez. Sistem kontrolleri Inventory kesintisini ve Orders yeniden başlatmasını kapsar.
