# Stok ve rezervasyon

Inventory kendi SQLite veritabanında ürün kimliği, fiziksel stok (`Quantity`), ayrılmış miktar (`Reserved`) ve kalıcı rezervasyon kayıtlarını tutar. Kullanılabilir miktar `Quantity - Reserved` olarak hesaplanır. Başka servisin veritabanına erişmez.

Mevcut yerel başlatma komutu: `powershell -File scripts/start-local.ps1` (katalog bağlantısı için docs/catalog.md). Inventory ayrıca `dotnet run --project src/Services/Inventory/Inventory.Api -- --urls http://localhost:5102` ile çalışır. Varsayılan dosya çalışma dizinindeki `inventory.db`; `ConnectionStrings__Inventory` ile örneğin `Data Source=D:/data/inventory.db;Default Timeout=30` seçilebilir. Boş veritabanı EF Core EnsureCreated ile oluşturulur; bu aşamada önceki Inventory şeması veya veri dönüşümü yoktur.

HTTP sözleşmesi: [inventory.openapi.yaml](../contracts/http/inventory.openapi.yaml).

1. `POST /stock/{productId}` ve `{"quantity":5}` stok kaydını bir defa oluşturur; mevcut ürünü değiştirmez (409).
2. `PUT /reservations/{reservationId}` ve `{"productId":"...","quantity":2}` stok ayırır. İstemci kalıcı, boş olmayan bir UUID seçer. Aynı kimlik ve içerik 200 ile güncel rezervasyon durumunu döndürür; farklı içerik 409 döndürür. Yetersiz stok veya tanınmayan ürün 409; geçersiz miktar 400 döndürür. Başarısız ayırma rezervasyon kaydı oluşturmaz.
3. `POST /reservations/{id}/commit` ayrılan miktarı fiziksel stoktan düşürür; `POST /reservations/{id}/release` ayrılan miktarı kullanılabilir hale getirir. Aynı işlem tekrarlandığında 200, karşıt son durum 409, bulunamayan rezervasyon 404 döndürür. Sonlanmış kayıtla yeniden ayırma yapılmaz. Kimliklerin tekrar kullanılmasını engellemek için kayıtlar silinmez.

Her değiştirme işlemi EF Core üzerinden SQLite serializable transaction kullanır. Sağlayıcı başlangıçta yazma kilidi alır; okuma ve güncellemeler diğer yazarlara karşı aynı işlem içindedir. Koşullu atomik güncelleme yalnızca yeterli kullanılabilir stok varsa ayırır. Stok ve rezervasyon kaydı birlikte commit edilir; hata halinde birlikte geri alınır. Veritabanı CHECK kısıtları `0 <= Reserved <= Quantity` koşulunu, pozitif rezervasyon miktarını ve geçerli durumları zorlar; rezervasyon kimliği birincil anahtardır, ürün ilişkisi yabancı anahtarla korunur.

SQLite aynı dosyayı kullanan servis süreçlerinde tutarlılık sağlar; tek yazarlı olduğu için yazma kapasitesi sınırlıdır. Çok makineli yatay ölçekleme için merkezi veritabanına geçiş gerekir. Kilit bekleme süresi dolarsa işlem başarısız olur; istemci aynı rezervasyon kimliğiyle tekrar deneyebilir. Ağ dosya sistemi üzerinde paylaşım kullanılmamalıdır.

Doğrulama (harici sunucu gerektirmez):

```powershell
dotnet test tests/Inventory.UnitTests
dotnet test tests/Inventory.IntegrationTests
```

Entegrasyon testleri gerçek dosya tabanlı SQLite ve aynı dosyayı kullanan iki bağımsız ASP.NET Core host üzerinde çalışır. Son stok yarışı, eşzamanlı aynı kimlik, yetersiz stok, değişen içerik, eşzamanlı/tekrarlı kesinleştirme ve bırakma, karşıt son durum yarışı ve doğrudan SQL kısıtı doğrulanır. Her kontrol yeni DbContext ile kalıcı durumu okur.

Order events use POST /internal/messages. Stock, Inbox, OrderDecisions and result Outbox commit before HTTP 204. Results retry through Orders__Url. Legacy broker-only messages are not migrated; queues are untouched.
