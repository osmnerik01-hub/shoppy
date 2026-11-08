# Katalog

Catalog PostgreSQL kullanır; Inventory ve Orders ayrı SQLite dosyaları kullanır.

.NET 10 SDK ve çalışan PostgreSQL 17+ gereklidir. Catalog için ayrı bir `catalog` veritabanı ve sahibi olan kullanıcı oluşturun. Bağlantıyı ortamınızda ayarlayın:

```powershell
$env:ConnectionStrings__Catalog = 'Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=YOUR_LOCAL_PASSWORD'
powershell -ExecutionPolicy Bypass -File ./scripts/start-local.ps1
```

Başlatma betiği migration uygular; migration içindeki sabit kimlikli iki örnek ürün yalnızca bir kez eklenir. Yeniden başlatma kullanıcı verisini değiştirmez. Üretimde migration ayrı dağıtım adımı olmalıdır; API varsayılan olarak migration çalıştırmaz. `Catalog__ApplyMigrations=true` yerel seçenek olarak kullanılabilir.

GET http://localhost:5101/products?page=1&pageSize=20 ve GET /products/{id}. Varsayılan sayfa 1, boyut 20; boyut en fazla 100. Kimliğe göre sıralama kararlıdır. Boş sayfa 200; bozuk kimlik/sayfalama 400; bulunamayan ürün 404; beklenmeyen hata 500 ProblemDetails döndürür. Yazma uçları bu aşamada yoktur. Ürün adı ve açıklama kırpılır, boş olamaz (200/4000 karakter). Fiyat pozitif, en fazla 9999999999.99 ve iki ondalıklı; para birimi TRY/USD/EUR olmalıdır.

Sözleşme: [catalog.openapi.yaml](../contracts/http/catalog.openapi.yaml).

Testler gerçek PostgreSQL üzerinde her çalışmada benzersiz geçici veritabanı oluşturur ve finally bloğunda kaldırır. Test kullanıcısının CREATEDB yetkisi gerekir. Veritabanı yoksa test atlanmaz, başarısız olur:

```powershell
$env:CATALOG_TEST_POSTGRES = 'Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=YOUR_LOCAL_PASSWORD'
powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
```

Migration üretimi: `dotnet tool install dotnet-ef --tool-path .tools --version 10.0.14`; ardından `.tools/dotnet-ef migrations add NAME --project src/Services/Catalog/Catalog.Infrastructure --startup-project src/Services/Catalog/Catalog.Infrastructure`. Tasarım zamanı bağlantısı ConnectionStrings__Catalog üzerinden okunur.

HTTP teslimatı için .NET 10 SDK (global.json), Node.js 24/npm ve PostgreSQL 17+ gerekir. SQLite ayrı servis gerektirmez. `ConnectionStrings__Catalog` bağlantısını ve migration yetkilerini ayarlayın. `scripts/start-local.ps1` veya mevcut veriler için `scripts/start-existing-local.cmd` çalıştırılır. SDK/Node, PostgreSQL, port ve mevcut SQLite dosyası kontrolleri korunur. Varsayılan arayüz http://localhost:5100 adresindedir; Ctrl+C uygulama süreçlerini kapatır.

Orders, `Inventory__Url` (varsayılan http://localhost:5102); Inventory, `Orders__Url` (varsayılan http://localhost:5103) kullanır. Başlatıcı seçilen portları aktarır. Worker kalıcı Outbox içeriğini `/internal/messages` ucuna POST eder; HTTP zaman aşımı 5 saniye, yeniden deneme aralığı 1 saniyedir. Sent yalnızca tüketici işlemi tamamlanıp başarılı yanıt alındıktan sonra yazılır. Inbox ve kalıcı sipariş kararları tekrar teslimatta, yanıt kaybında ve süreç yeniden başladığında ikinci stok etkisini engeller. Desteklenmeyen olay/sürüm 400 alır ve operatör sözleşmeyi düzeltene kadar bekler. İç uçlar güvenilir servis ağında tutulmalıdır; Gateway bunları yayınlamaz.

RabbitMQ artık gerekli değildir. Servisine ve eski kuyruklarına dokunulmaz. Yalnızca eski broker kuyruğunda kalan mesajlar otomatik taşınmaz. Sent=false Outbox kayıtları HTTP ile gönderilir; Sent=true kayıtlar yeniden açılmaz. SQLite ve PostgreSQL verileri korunur. `scripts/control-local.ps1` inventory/orders yeniden başlatma ve shutdown işlemlerini destekler. CI yalnızca `CATALOG_TEST_POSTGRES` ve `CATALOG_CONNECTION_STRING` bağlantı sırlarını gerektirir; broker CLI veya URI/vhost ayarı gerekmez. Sistem kontrolleri Inventory kesintisini ve Orders yeniden başlatmasını kapsar.

## 2026-11-04: Catalog discovery

Optional query parameters: search (literal case-insensitive name/description search, up to 200 characters), currency (TRY/USD/EUR), minPrice, maxPrice, sort (name/name-desc/price/price-desc). Price filtering and sorting require currency. Bounds are inclusive, from 0 to 9999999999.99; minPrice must not exceed maxPrice. Repeated known parameters return 400. Filtering and sorting execute in PostgreSQL before pagination. totalCount counts filtered rows; ties use product id ascending. Requests without filters retain their response and default order.

The AngularJS view resets to page one on criteria changes, discards stale responses, displays result counts and separate empty/error/loading states, and provides filter reset. Price controls require a selected currency. Existing cart and order tracking remain available. System tests accept INVENTORY_URL for existing installations using custom ports.
