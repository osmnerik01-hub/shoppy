# Mimari temel

Bu aşama .NET 10 LTS üzerinde bağımsız derlenebilir ve dağıtılabilir üç ASP.NET Core servisinin temelini kurar. Paket sürümleri Directory.Packages.props, ortak derleme ayarları Directory.Build.props içinde tutulur. AngularJS arayüzü Gateway üzerinden sunulur; servisler HTTP ve RabbitMQ ile haberleşir.

## Sahiplik ve veri sınırları

| Servis | Sahip olduğu veri ve karar | Yerel port | Ayrılmış veritabanı adı |
| --- | --- | --- | --- |
| Catalog | Ürün kimliği, açıklaması, satış fiyatı | 5101 | PostgreSQL catalog |
| Inventory | Ürün kimliğine göre kullanılabilir stok ve rezervasyonlar | 5102 | inventory.db |
| Orders | Sipariş, kalemler, sipariş anındaki fiyat ve durum | 5103 | orders.db |

Her servis kendi Domain, Application, Infrastructure ve Api projelerine sahiptir. Domain iş kurallarını, Application kullanım durumlarını ve portları, Infrastructure EF Core bağdaştırıcılarını, Api HTTP girişini ve bağımlılık bileşimini barındırır. Application yalnızca Domain'e; Infrastructure Application/Domain'e; Api Application/Infrastructure'a başvurabilir. Domain EF Core, ASP.NET Core ve HTTP istemcisi bağımlılığı taşımaz.

Servisler başka servisin projesini, DbContext'ini, tablolarını, bağlantı bilgisini veya migration'larını paylaşamaz. Ürün kimliği diğer servislerde yalnızca bir dış kimliktir; servisler arası foreign key ve join yoktur. İleride iletişim sürümlenmiş HTTP sözleşmeleri veya olaylarla yapılır; ortak domain modeli paylaşılmaz. Her servis kendi veritabanı kimlik bilgilerini ve migration yaşam döngüsünü yönetir. Üretimde veritabanı erişim izinleri de bu ayrımı uygulamalıdır; kaynak mimari testleri çalışma zamanı SQL erişim yetkilerinin yerine geçmez.

Catalog PostgreSQL kullanır. Inventory ve Orders ayrı SQLite dosyalarında EnsureCreated ile kendi tablolarını oluşturur. Orders fiyatı Catalog HTTP API'sinden alır, siparişi ve outbox kaydını aynı transaction içinde yazar. RabbitMQ üzerinden Inventory'ye ulaşan sipariş atomik stok kontrolü ve inbox ile işlenir; sonuç Orders'a outbox üzerinden iletilir. Tekrarlar ikinci stok düşümüne yol açmaz. Kesintide iş Pending kalır ve servisler geri geldiğinde yeniden denenir.

## Yerel çalışma

Güncel bağlantılar ve kurulum [Windows kılavuzunda](local-windows.md). `scripts/start-local.ps1` Web'i derler; Catalog, Inventory, Orders ve Gateway süreçlerini başlatır. Arayüz http://localhost:5100 adresindedir. Ctrl+C başlatılan süreçleri durdurur. PostgreSQL/RabbitMQ ayrı Windows servisleridir. Inventory/Orders ayrı SQLite dosyalarını `artifacts/local/data` altında tutar. Her API `/health/live` ve `/health/ready` sunar.

`./scripts/verify.ps1` derleme, .NET ve Web testlerini; `-System` gerçek yerel sistem ve kesinti kontrollerini çalıştırır. Architecture.Tests katman/veri sınırlarını denetler. İsteğe bağlı iz aktarımı [Gateway](gateway.md) belgesindedir.
