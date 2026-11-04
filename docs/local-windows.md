# Windows üzerinde Docker olmadan çalıştırma

Gerekenler: global.json ile uyumlu .NET 10 SDK (10.0.100 veya sonraki 10.0 feature band), Node.js 24 LTS/npm, çalışan PostgreSQL 17+ ve RabbitMQ (Windows kurulumunun gerektirdiği Erlang dahil). SQLite için ayrı servis gerekmez. PostgreSQL ve RabbitMQ Windows servislerini kendi kurulum yönergeleriyle başlatın; betik bunları kurmaz veya durdurmaz. OTEL collector isteğe bağlıdır.

PostgreSQL yönetim aracında uygulamaya özel kullanıcı ve veritabanı oluşturun (parolayı değiştirin):

```sql
CREATE ROLE catalog LOGIN PASSWORD 'YOUR_LOCAL_PASSWORD';
CREATE DATABASE catalog OWNER catalog;
```

Mevcut veritabanını kullanıyorsanız kullanıcı migration için şema oluşturma/değiştirme yetkisine sahip olmalıdır. RabbitMQ üzerinde kullanıcıya seçilen vhost için configure/write/read izni verin. Yerel varsayılan guest kullanıcısı yalnızca localhost bağlantısında kullanılabilir.

Proje kökünde PowerShell:

```powershell
$env:ConnectionStrings__Catalog = 'Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=YOUR_LOCAL_PASSWORD;Timeout=5'
$env:RabbitMQ__Uri = 'amqp://guest:guest@localhost:5672/'
powershell -ExecutionPolicy Bypass -File ./scripts/start-local.ps1
```

RabbitMQ URI içindeki özel karakterleri URL kodlayın. Bağlantıları kaynak dosyalara yazmayın. Betik npm ci, Web derlemesi ve .NET derlemesini yapar. Catalog migration ve demo ürünlerini hazırlar; sonra Inventory, Orders ve Gateway'i sırayla başlatır ve her birinin /health/ready yanıtını bekler. Inventory varsayılan olarak eksik demo stoklarını ekler; mevcut stok miktarını sıfırlamaz. Bunu kapatmak için Inventory__SeedDemo=false ayarlayın.

Arayüz http://localhost:5100, Catalog 5101, Inventory 5102, Orders 5103 portundadır. -GatewayPort, -CatalogPort, -InventoryPort ve -OrdersPort ile portları değiştirebilirsiniz; Gateway ve Orders hedefleri aynı değerlere otomatik ayarlanır. Her servis /health/live ve /health/ready sunar. -StartupTimeoutSeconds (varsayılan 90) servis başına beklemeyi belirler.

Varsayılan SQLite dosyaları artifacts/local/data içindedir. ConnectionStrings__Inventory ve ConnectionStrings__Orders ile ayrı kalıcı dosyalar seçilebilir; mutlak dosya yolları ve mevcut, yazılabilir üst dizin kullanın. Önceden farklı çalışma dizininde oluşturulan .db dosyalarını kullanmak için bu değişkenleri eski dosyaların mutlak yollarına ayarlayın. Betik veri silmez.

Ctrl+C, başlangıç hatası veya uygulama kapanması durumunda yalnızca bu betiğin başlattığı uygulamalar durdurulur. PostgreSQL ve RabbitMQ çalışmaya devam eder. Uygulamalar sonlandırıldığı için devam eden HTTP isteği kesilebilir; kalıcı inbox/outbox yeniden başlatmada işlenir. PowerShell penceresini zorla kapatmak yerine Ctrl+C kullanın. -StopAfterReady tüm servisleri hazır bulduktan sonra kapatan duman testi seçeneğidir. Loglar her çalıştırma için artifacts/local/logs altında ayrı dizindedir; hata halinde Catalog logunda PostgreSQL erişimini/migration yetkisini, Inventory/Orders loglarında broker kullanıcı ve vhost yetkilerini kontrol edin. Loglar bağlantı hatası ayrıntıları içerebilir; paylaşmadan önce inceleyin.

Yerel PostgreSQL üzerinde geçici veritabanı oluşturup silebilen test hesabını ayarlayın:

```powershell
$env:CATALOG_TEST_POSTGRES = 'Host=localhost;Database=postgres;Username=postgres;Password=YOUR_LOCAL_PASSWORD'
./scripts/verify.ps1
# Normal başlatıcı kapalıyken ve uygulama bağlantıları ayarlanmış olarak:
./scripts/verify.ps1 -System
```

İlk komut .NET, Web birim/sözleşme ve taklit API tarayıcı kontrollerini çalıştırır. `-System` yerel sistemi başlatır; gerçek alışveriş, Inventory/RabbitMQ kesintisi, Orders yeniden başlatma ve idempotency kontrollerini çalıştırır; uygulamaları kapatır. RabbitMQ sbin PATH'te olmalı ve aynı kullanıcıyla `rabbitmqctl.bat status` çalışmalıdır. Testler stok tüketir; ayrılmış demo verilerinde klavye stoğu en az 3 olmalıdır. Veriler sıfırlanmaz.

CI aynı betiği ayrılmış yerel Windows runner üzerinde çalıştırır; hazırlık [yerel kılavuzda](local-windows.md). Raporlar, ekran görüntüleri ve servis logları `artifacts` altında, CI'da `verification-results` artifact'ında saklanır. Paylaşmadan önce loglardaki bağlantı ayrıntılarını inceleyin.

## CI için yerel runner

Workflow `self-hosted`, `Windows`, `project1-local` etiketli, bu projeye ayrılmış Windows runner gerektirir. PostgreSQL ve RabbitMQ/Erlang yukarıdaki gibi kurulmalıdır. RabbitMQ sbin runner PATH'inde olmalı, node Erlang cookie'sine runner hesabı erişebilmelidir; `rabbitmqctl.bat status` ile doğrulayın. Portlar 5100–5103 boş olmalıdır. .NET/Node workflow tarafından hazırlanır.

Repository secrets: `CATALOG_TEST_POSTGRES` (CREATEDB yetkili test bağlantısı), `CATALOG_CONNECTION_STRING` (ayrılmış catalog veritabanı sahibi), `RABBITMQ_URI` (yerel broker/vhost). Kalıcı stoklar testlerde tüketilir; her çalıştırmadan önce stok yeterliliğini sağlayın veya ayrılmış test verilerini hazırlayın. CI kullanıcı verisini silmez. Güvenilmeyen pull request'leri bu runner'a göndermeyin.
