# Katalog

Catalog PostgreSQL kullanır; Inventory ve Orders ayrı SQLite dosyaları kullanır.

.NET 10 SDK ve çalışan PostgreSQL 17+ gereklidir. Catalog için ayrı bir `catalog` veritabanı ve sahibi olan kullanıcı oluşturun. Bağlantıyı ortamınızda ayarlayın:

```powershell
$env:ConnectionStrings__Catalog = 'Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=YOUR_LOCAL_PASSWORD'
$env:RabbitMQ__Uri = 'amqp://guest:guest@localhost:5672/'
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
