# Mini e-ticaret

ASP.NET Core / EF Core (.NET 10), AngularJS 1.8.3 ile katalog, stok ve sipariş demosu.

## Yerelde çalıştırma

Gerekenler: Windows PowerShell 5.1+, global.json ile uyumlu .NET 10 SDK, Node.js 24 LTS/npm, PostgreSQL 17+ ve Erlang ile RabbitMQ. SQLite için ayrı servis gerekmez.

PostgreSQL ve RabbitMQ servislerini başlatın. PostgreSQL üzerinde uygulamaya özel `catalog` kullanıcısı ve bu kullanıcıya ait `catalog` veritabanını oluşturun; kullanıcı migration için şema değiştirebilmelidir. RabbitMQ kullanıcısına seçilen vhost üzerinde configure/write/read izni verin (yerel `guest` yalnızca localhost içindir). Ayrıntılı hazırlık: [Windows yerel kurulum kılavuzu](docs/local-windows.md).

Proje kökünde bağlantıları ayarlayıp uygulamayı başlatın:

```powershell
$env:ConnectionStrings__Catalog = 'Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=YOUR_LOCAL_PASSWORD;Timeout=5'
$env:RabbitMQ__Uri = 'amqp://guest:guest@localhost:5672/'
powershell -ExecutionPolicy Bypass -File ./scripts/start-local.ps1
```

Başlatıcı bağımlılıkları yükler, Web/.NET derlemelerini yapar ve servislerin hazır olmasını bekler. Arayüz: http://localhost:5100. Durdurmak için aynı pencerede **Ctrl+C** kullanın; PostgreSQL ve RabbitMQ çalışmaya devam eder. `ConnectionStrings__Inventory` ve `ConnectionStrings__Orders` isteğe bağlı SQLite yollarıdır; varsayılan kalıcı dosyalar `artifacts/local/data` altındadır.

Doğrulama için yerel PostgreSQL üzerinde geçici veritabanı oluşturup silebilen (`CREATEDB`) test hesabını ayarlayın:

```powershell
$env:CATALOG_TEST_POSTGRES = 'Host=localhost;Database=postgres;Username=postgres;Password=YOUR_LOCAL_PASSWORD'
./scripts/verify.ps1
# Normal başlatıcı kapalıyken ve uygulama bağlantıları ayarlanmış olarak:
./scripts/verify.ps1 -System
```

İlk komut .NET, Web birim/sözleşme ve taklit API tarayıcı kontrollerini çalıştırır. `-System` yerel sistemi başlatır; gerçek alışveriş, Inventory/RabbitMQ kesintisi, Orders yeniden başlatma ve idempotency kontrollerini çalıştırır; uygulamaları kapatır. RabbitMQ sbin PATH'te olmalı ve aynı kullanıcıyla `rabbitmqctl.bat status` çalışmalıdır. Testler stok tüketir; ayrılmış demo verilerinde klavye stoğu en az 3 olmalıdır. Veriler sıfırlanmaz.

CI aynı betiği ayrılmış yerel Windows runner üzerinde çalıştırır; hazırlık [yerel kılavuzda](docs/local-windows.md). Raporlar, ekran görüntüleri ve servis logları `artifacts` altında, CI'da `verification-results` artifact'ında saklanır. Paylaşmadan önce loglardaki bağlantı ayrıntılarını inceleyin.

## Yapılandırma ve demo sınırları

Ortam değişkenleri ConnectionStrings__Catalog, ConnectionStrings__Inventory, ConnectionStrings__Orders, RabbitMQ__Uri, Catalog__Url ve ReverseProxy__Clusters__... değişkenleri bağlantıları belirler. Yerel Inventory API http://localhost:5102 yalnızca stok doğrulaması/yönetimi için açılır; internete sunmayın. Demo parolaları yalnızca yerel kullanım içindir. OTEL isteğe bağlıdır; [Gateway](docs/gateway.md) yerel collector adımlarını içerir.

Kimlik doğrulama, ödeme, kargo ve üretim operasyonları kapsam dışıdır. Sepetteki her ürün ayrı sipariştir; sepet bütünü için atomiklik yoktur. SQLite kalıcı dosyası tek host demosudur; bağımsız hostlarda yatay ölçekleme için paylaşımlı sunucu veritabanı ve migration stratejisi gerekir. Inventory/Orders EnsureCreated şeması otomatik şema yükseltmesi sağlamaz. AngularJS kullanım ömrünü tamamlamıştır. Inbox/outbox temizliği, zehirli mesaj yönetimi ve üretim sır yönetimi ayrıca ele alınmalıdır.

Ayrıntılar: [mimari](docs/architecture.md), [Catalog](docs/catalog.md), [Inventory](docs/inventory.md), [Orders](docs/orders.md), [Gateway](docs/gateway.md), [Web](docs/web.md).

