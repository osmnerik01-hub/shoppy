# Yerel doğrulama kaydı

## 2026-11-04 — yönetmen revizyonu

README'ye Windows önkoşulları, PostgreSQL/RabbitMQ hazırlığı, bağlantı değişkenleri, start-local.ps1 komutu, Ctrl+C ile durdurma ve http://localhost:5100 adresi eklendi.

Gerçek komut: `./scripts/verify.ps1 -System`. Sonuç: **başarısız (çıkış kodu 1)**. Testler veya test yapılandırmaları değiştirilmedi; başarısız kontroller atlanmadı.

Hazırlık: projeye ayrılmış yerel PostgreSQL 17.6 sunucusu 127.0.0.1:55432 üzerinde kullanıldı. Test hesabının CREATEDB yetkisi SQL ile doğrulandı. Uygulama için ayrı `project1_revision` veritabanı ve `artifacts/revision-data` altında yeni Inventory/Orders SQLite yolları ayrıldı. RabbitMQ 4.4.0, Erlang/OTP 29.1.2 ile yerel olarak başlatıldı; `rabbitmqctl.bat status` başarılıydı. Broker AMQP bağlantısı 127.0.0.1:5672 üzerinde dinliyordu. Node.js v24.14.1 kullanıldı.

- .NET çözüm derlemesi: başarılı, 0 uyarı / 0 hata.
- .NET testleri: 35 başarılı, 2 başarısız, 0 atlanan.
- `OrderFlowTests.Snapshot_idempotency_outage_redelivery_and_stock(stock: 5, status: "Confirmed", remaining: 3)`: Orders.IntegrationTests/OrderFlowTests.cs:46 üzerinde Assert.False beklentisi başarısız; gerçek değer True.
- `HealthTests.Missing_broker_prevents_orders_and_inventory_readiness`: ServiceHealth.Tests/HealthTests.cs:42 üzerinde beklenen ServiceUnavailable (503), gerçek OK (200).
- Catalog PostgreSQL entegrasyon testi dahil diğer .NET kontrolleri geçti.
- Betik .NET test hatasında durdu. Bu çalıştırmada Web derlemesi/birim testleri, taklit API tarayıcı testleri ve gerçek sistem/kesinti tarayıcı testleri **çalışmadı**; başarılı sayılmaz.

Tam komut çıktısı: [revision-verify-system.log](../artifacts/revision-verify-system.log). TRX sonuçları `artifacts/dotnet` altında 2026-11-04 01:40 zaman damgalı dosyalardadır. Uygulama başlatma aşamasına ulaşılmadı.

Docker'a özel `.dockerignore`, `compose.yaml` ve `deploy/Dockerfile` dosyalarının kaldırılmış olduğu doğrulandı. Etkin kaynak/belge taramasında eski Docker komutu, konteyner servis adresi veya silinen dosyalara referans bulunmadı. İsteğe bağlı collector yapılandırması yerelde 127.0.0.1:4317 dinliyor ve docs/gateway.md içindeki komut mevcut dosyaya işaret ediyor.

## Önceki aşamanın kaydı

Önceki aşamada Web derlemesi, 4 birim/sözleşme ve 10 taklit API tarayıcı testi geçmişti. PostgreSQL bağlantısı eksik olduğundan o aşamada tam sistem doğrulaması yapılamamıştı. Bu sonuçlar yukarıdaki güncel çalıştırmanın sonuçları değildir.