# 2026-11-04 ? Mevcut veriyle eri?ilebilirlik a?amas?

Ba?lat?lan komut (proje k?k?nde):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-existing-local.ps1 -ControlDirectory artifacts/local/existing-control
```

Servisler ?al???r b?rak?ld?. Kullan?c?n?n terminalden normal ba?latmas? i?in `-ControlDirectory` gerekli de?ildir. Bu oturumun arka plan ba?lat?c?s?n? kontroll? durdurmak i?in:

```powershell
$env:LOCAL_CONTROL_DIRECTORY = "$PWD/artifacts/local/existing-control"
./scripts/control-local.ps1 -Action shutdown
```

Kullan?lan mevcut dosyalar: `PRIVATE_LOCAL_PATH\artifacts\revision-data\inventory.db` ve `orders.db`. ?zole testlerin `verification-final-data` veya `acceptance_*` dosyalar? kullan?lmad?. Kaynak dizinindeki Inventory dosyas?nda stok yoktu, Orders dosyas? bulunmad?; bu dosyalar yarat?lmad?, de?i?tirilmedi veya silinmedi. Revision ?iftinin ?nceki kullan?m kayd? `/` vhost'una i?aret ediyor; ancak bu vhost t?m mesajlar? bak?m?ndan temiz bir e?le?me de?ildir (a?a??daki s?n?rlamaya bak?n).

PostgreSQL `127.0.0.1:55432/project1_revision`, RabbitMQ `project1@localhost`, AMQP `127.0.0.1:5672`, vhost `/`. ?ki ba??ml?l?k da eri?ilebilir. ?lk broker ba?latma denemesi zaten ?al??an d???m?n da??t?m portuyla ?ak??t?; mevcut d???m korundu, yeniden olu?turulmad?. Tan? ??kt?lar? `artifacts/local/broker-resume*` alt?nda.

Ba?lat?c? `ConnectionStrings__Inventory/Orders` de?erlerini mutlak dosya yollar?na, `Mode=ReadWrite` ile ayarl?yor; demo stok ekleme kapal?. Yerel servisler aras? hedefler IPv4 kullan?yor. ?lk ?al??mada Gateway'in iki saniyelik ba??ml?l?k kontrol? `localhost` ba?lant?lar?yla 503 verdi; IPv4 hedefleriyle ayn? kontrol de?i?tirilmeden ba?ar?l? oldu.

- Web ve .NET derlemesi ba?ar?l?; .NET: 0 uyar?, 0 hata. ?nceden ba?ar?l? testler yeniden ?al??t?r?lmad?.
- 5100, 5101, 5102, 5103 `/health/ready`: hepsi HTTP 200 / Healthy.
- Gateway `/api/products`: HTTP 200, mevcut iki ?r?n; Gateway `/`: HTTP 200, HTML.
- Inventory `/stock/11111111-1111-1111-1111-111111111111`: HTTP 200, quantity/available 96, reserved 0.
- Orders `/orders/5c359c67-e258-4a0d-b61d-45bebf6b3876`: HTTP 200, mevcut Pending sipari?.
- SQLite salt okuma b?t?nl?k kontrolleri: ikisi de `ok`. Ba?lang?? ve son tablo i?erikleri tamamen ayn?; iki stok ve d?rt sipari? korundu. Yeni sipari?, stok g?ncelleme veya kesinti testi yap?lmad?.

Kan?tlar: [son GET yan?tlar?](../artifacts/local/existing-readonly-http-final.json), [ba?lang?? veri g?r?n?m?](../artifacts/local/existing-data-before.json), [son veri kar??la?t?rmas?](../artifacts/local/existing-data-after.json), [broker sorgular?](../artifacts/local/existing-broker-queues.log), [ba?latma ??kt?s?](../artifacts/local/existing-start-ipv4.out.log). Servis loglar? `artifacts/local/logs/41d9e4421b6141bd8b18408bd44544cd` alt?nda.

**Kalan s?n?rlama:** `/` vhost'undaki `orders.results.v1` kuyru?unda be? bekleyen mesaj var. Orders ?al??an? mevcut veritaban?nda kar??l??? bulunmayan bir sonu? mesaj?nda `Sequence contains no elements` ile yeniden deniyor; d?rt sipari? Pending kal?yor. Mesajlar silinmedi, onaylan?p kaybedilmedi veya ba?ka vhost'a ta??nmad?. Bu a?aman?n haz?r olma ve salt okuma kabul kontrolleri ge?ti; mevcut kuyru?un sorunsuz i?lenmesi ve yeni sipari? ak??? do?rulanm?? de?ildir. Kay?p sipari?i uydurmak veya mesaj? d???rmek yerine bu veri/broker tutars?zl??? a??k b?rak?ld?. npm AngularJS kullan?mdan kald?rma uyar?s? verdi; ba??ml?l?klar bu a?amada y?kseltilmedi.

---

# Yerel doğrulama kaydı

## 2026-11-04 — RabbitMQ ortam yalıtımı sonrası tam doğrulama

`scripts/verify.ps1`, uygulamanın `RabbitMQ__Uri` değerini .NET testlerinden önce saklar, test çağrısı sırasında ortamdan kaldırır ve `finally` içinde geri yükler. Test dosyaları ve beklentileri değiştirilmedi. Değerin mevcut ve tanımsız olduğu durumlarda hem başarı hem hata yolundaki geri yükleme ayrıca kontrol edildi: [yalıtım kaydı](../artifacts/rabbitmq-uri-isolation-check.log).

`./scripts/verify.ps1 -System` yeniden çalıştırıldı: **başarılı, çıkış kodu 0**. [Tam komut çıktısı](../artifacts/revision-verify-system-clean.log) tüm derleme ve test sonuçlarını, ayrıca uygulama RabbitMQ URI değerinin geri yüklendiği kontrolünü içerir.

- .NET derlemesi: 0 uyarı, 0 hata; 37 test başarılı, 0 başarısız, 0 atlanan. Önceki iki başarısız test de geçti.
- Web derlemesi başarılı; 4 birim/sözleşme ve 10 taklit API tarayıcı testi geçti.
- Gerçek sistem: 3 test geçti; arayüzden onay/yetersiz stok, stok düşümü, Inventory ve RabbitMQ kesintilerinden kurtarma, Orders yeniden başlatma ve idempotency doğrulandı.
- Kapanıştan sonra 5100–5103 portlarında dinleyen uygulama kalmadı. PostgreSQL ve RabbitMQ korundu.

Gerçek servisler: PostgreSQL 17.6, 127.0.0.1:55432; SQL ile test hesabının CREATEDB yetkisi ve `project1_revision` veritabanı doğrulandı. RabbitMQ 4.4.0 düğümü `project1@localhost`, AMQP 127.0.0.1:5672; CLI için `ERLANG_HOME=C:\Program Files\Erlang OTP` ve `RABBITMQ_NODENAME=project1@localhost` kullanıldı. [Servis doğrulama çıktısı](../artifacts/revision-local-services.log) ilk hatalı CLI denemelerini ve ardından başarılı durum sorgusunu içerir.

Bu revizyondaki ilk tekrar .NET/Web kontrollerini geçti ancak 3 sistem testi başarısız oldu: Orders loglarında mevcut kuyruktaki mesajlar için `Sequence contains no elements`, ayrıca eksik Erlang/düğüm ortamı nedeniyle RabbitMQ kontrol hatası görüldü. [İlk tekrarın sistem raporu](../artifacts/verification-first-attempt/system.xml) ve ekran görüntüleri/izleri aynı dizinde korundu. Eski veriler silinmedi. Başarılı tam tekrar için `project1_verify_20261104_1600` adlı ayrı vhost, guest kullanıcısına bu vhost üzerinde izin ve `artifacts/verification-final-data` altında yeni Inventory/Orders SQLite dosyaları kullanıldı; Catalog aynı ayrılmış veritabanında kaldı.

Sonuçlar: .NET TRX dosyaları `artifacts/dotnet` altında 16:01 zaman damgalıdır; [sistem JUnit](../artifacts/system.xml), HTML raporu `artifacts/system-report`, ekran görüntüleri `artifacts/system-results`, servis logları `artifacts/local/logs/12b96230503540948fe728c0b13f7588` altındadır. npm mevcut bağımlılıklar için 1 moderate ve 1 high güvenlik bulgusu bildirdi; bağımlılıklar değiştirilmedi.

Kabul kriterleri yeniden kontrol edildi: Docker'a özel dosyalar yok; CI yerel Windows servisleriyle aynı betiği kullanıyor; README hazırlık/başlatma/durdurma ve gerçek arayüz adresini içeriyor. Etkin kaynak ve belgelerde Docker komutu, konteyner servis adresi veya kaldırılmış dosyaya etkin referans bulunmadı. Collector temel çalışma için gerekmiyor; isteğe bağlı yerel yapılandırması ve kılavuzdaki mevcut dosya bağlantısı korundu.

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

## 2026-11-04 — Kilitli DLL sonrası kontrollü yeniden derleme

Yalnızca başlatıcının `existing-control` kanalına `shutdown` gönderildi; Catalog, Inventory, Orders ve Gateway süreçlerinin kapandığı doğrulandı. PostgreSQL ve RabbitMQ durdurulmadı. Ardından `dotnet build Project1.sln` çalıştırıldı: **çıkış kodu 0, 0 uyarı, 0 hata**. Tam çıktı: [build kaydı](../artifacts/local/rebuild-after-shutdown.log); [işlem/çıkış kodu kaydı](../artifacts/local/rebuild-resume-status.log).

Aynı mevcut profil şu komutla yeniden başlatıldı:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-existing-local.ps1 -ControlDirectory artifacts/local/existing-control
```

Profil yine `artifacts/revision-data/inventory.db` ve `orders.db` mutlak yollarını, `Mode=ReadWrite`, demo stok ekleme kapalı, PostgreSQL `127.0.0.1:55432/project1_revision` ve RabbitMQ `/` vhost ayarlarını kullanıyor. Veri dosyası veya kuyruk silinmedi/sıfırlanmadı; testler ve test yapılandırması değiştirilmedi, başarılı testler tekrar çalıştırılmadı. Başlatma çıktısı: [stdout](../artifacts/local/rebuild-resume.out.log), [stderr](../artifacts/local/rebuild-resume.err.log). Önceki kayıtta belirtilen eşleşmeyen eski Orders mesajı sınırlaması devam eder.

Yeniden başlatma tamamlandı ve servisler çalışır bırakıldı. 5100–5103 /health/ready, Gateway /api/products ve mevcut Inventory /stock/11111111-1111-1111-1111-111111111111 GET yanıtlarının tamamı HTTP 200. Salt okuma yanıtları: [JSON kaydı](../artifacts/local/rebuild-resume-readonly.json). Servis logları: artifacts/local/logs/dcf7c914e2004c00a381c930d64b6585.

## 2026-11-04 — 56056 portu ve kapanış doğrulaması

İki kez kullanılan komut (proje kökünde):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/start-existing-local.ps1 -GatewayPort 56056 -ControlDirectory artifacts/local/existing-control
```

İlk çalışma eski başlatıcının, ikinci çalışma güncellenmiş başlatıcının dört uygulama sürecinin çıkışını doğruladı: `Shutdown verified: 4 application processes exited.` Her iki çalışmada kapanış doğrulamasından sonra Web/.NET derlemesi başladı; .NET: 0 uyarı, 0 hata. stdout/stderr kayıtlarında MSB3026 veya kilitli DLL hatası yok. Önceden başarılı test paketleri tekrar çalıştırılmadı. Üç PowerShell betiği sözdizimi kontrolünden geçti. Erken yapılandırma hatasının mevcut kontrol işaretini kaldırmadığı ayrıca doğrulandı.

İlk ve son GET kontrolleri: http://127.0.0.1:56056/ HTTP 200 text/html; http://127.0.0.1:56056/health/ready HTTP 200; http://127.0.0.1:56056/api/products HTTP 200 JSON. Bu adreste bağlantı reddi oluşmadı. Servisler 56056/5101/5102/5103 üzerinde çalışır bırakıldı.

Mevcut artifacts/revision-data/inventory.db ve orders.db, PostgreSQL 127.0.0.1:55432/project1_revision ve RabbitMQ 127.0.0.1:5672/ kullanıldı. SQLite dosyası veya RabbitMQ mesajı silen/sıfırlayan komut çalıştırılmadı; bağımlılıklar ve ilgisiz süreçler durdurulmadı. Önceki eski Orders mesajı sınırlaması bu aşamada değiştirilmedi.

Kanıtlar: [ilk başlatma](../artifacts/local/preview-port-start.out.log), [ilk stderr](../artifacts/local/preview-port-start.err.log), [yeniden başlatma](../artifacts/local/preview-port-restart.out.log), [yeniden başlatma stderr](../artifacts/local/preview-port-restart.err.log), [ilk HTTP](../artifacts/local/preview-port-http-first.json), [son HTTP](../artifacts/local/preview-port-http-final.json), [hata yolu](../artifacts/local/preview-port-failure-check.log). Son servis logları: artifacts/local/logs/f182bd71e7c94a648173f2ca503faabf.

## 2026-11-04 — Son teslim kontrolü (director revision)

Bu kayıt, yukarıdaki “servisler çalışır bırakıldı” son durumunu günceller: teslimde dört uygulama kapalıdır. GatewayPort aktarımı ve süreç çıkışını bekleyen mevcut betik düzeltmeleri korundu; test yapılandırması değiştirilmedi ve önceki başarılı test paketleri tekrarlanmadı.

Kapatmadan hemen önce hedefli GET kontrolü: `http://127.0.0.1:56056/` **200 text/html**, `/health/ready` **200**, `/api/products` **200 application/json**; bağlantı reddi yok. [HTTP kaydı](../artifacts/local/preview-port-delivery-http.json).

Proje kökünde sırasıyla çalıştırılan son kontrol komutları:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/control-local.ps1 -Action shutdown -ControlDirectory artifacts/local/existing-control
dotnet build Project1.sln
```

Kontrol kanalı `Shutdown verified: 4 application processes exited.` döndürdü; proje uygulaması süreç sayısı 0 olarak ayrıca doğrulandı. Bundan sonra zorunlu çözüm derlemesi çalıştı: **çıkış kodu 0, 0 uyarı, 0 hata**. MSB3026/MSB3027/MSB3021 yok. [Kapanış](../artifacts/local/preview-port-delivery-shutdown.log), [derleme](../artifacts/local/preview-port-delivery-build.log), [çıkış kodu](../artifacts/local/preview-port-delivery-status.log).

Üç ilgili PowerShell betiğinin sözdizimi kontrolü geçti. Son kontrolde 56056/5101/5102/5103 dinleyicileri yoktu; PostgreSQL 127.0.0.1:55432 (PID 22948) ve RabbitMQ 127.0.0.1:5672 (PID 17996) dinliyordu. Uygulamalar derlemeden sonra yeniden başlatılmadı. Mevcut SQLite dosyaları ve RabbitMQ mesajları silinmedi/sıfırlanmadı; bağımlılık servisleri ve ilgisiz süreçler durdurulmadı.

## 2026-11-04 — İşlem kapsamlı CMD başlatıcı doğrulaması

Onaylanmış aşama kapsamında yalnızca başlatıcılar ve belgeler değiştirildi. Windows PowerShell salt okunur sorgusunda MachinePolicy/UserPolicy/Process/CurrentUser/LocalMachine tümü Undefined, etkin politika Restricted bulundu: bildirilen PSSecurityException ile uyumlu engelin kaynağı Windows PowerShell varsayılan politikasıdır. PowerShell 7 terminalinde LocalMachine RemoteSigned görülmesi Windows PowerShell sonucunu değiştirmiyor. Kaynak betikte Zone.Identifier bulunmadı. Kalıcı politika veya dosya engeli değiştirilmedi. Son sorgu da aynı sonucu verdi: [kapsam kaydı](../artifacts/local/policy-scopes-after.log).

Yeni CMD başlatıcısı boşluk içeren `artifacts/local/launcher checks` dizinine kopyalanarak yalnızca deneme betiğiyle sınandı: Process=Bypass, GatewayPort, üç servis portu, boşluklu ControlDirectory, timeout ve StopAfterReady aktarımı doğrulandı; başarı kodu 0 ve hata kodu 23 korundu. İlk denemede PowerShell 7'den devralınan modül yolları Windows PowerShell modül yüklemesini bozdu; başlatıcının yerel PSModulePath ayarı sonrası hata düzeltilerek kontrol yeniden geçti. [Hedefli kayıt](../artifacts/local/launcher-focused-check.log). Gerçek betikte geçersiz GatewayPort=0 ve dolu 5100 portu sıfırdan farklı kod verdi: [parametre](../artifacts/local/policy-invalid-port.log), [port](../artifacts/local/policy-occupied-port.log). Üç ilgili PS1 sözdizimi kontrolü geçti. Kurumsal politika uygulanmış bir makine bulunmadığından GPO engeli dinamik olarak sınanmadı; önceliği belgelenip hata önerisine eklendi.

Mevcut kontrol işaretindeki PID 27268 çalışmıyordu; çalışan uygulamalar farklı ebeveyn PID 16640'a aitti. Bu nedenle bu kanalla deneme durduruldu; çalışan uygulamalar öldürülmedi. Kontrol betiği artık çalışmayan kontrol PID'sinde açık Türkçe hata verir. Eski işaret veya kullanıcı verileri silinmedi.

Gerçek yeni başlatıcı `-GatewayPort 56056 -CatalogPort 56101 -InventoryPort 56102 -OrdersPort 56103 -ControlDirectory artifacts/local/policy-control` ile çalıştırıldı. Başlatıcının gerekli Web/.NET derlemeleri tamamlandı (.NET 0 uyarı, 0 hata); önceki başarılı test paketleri tekrarlanmadı. Docker aranmadı. Mevcut SDK/Node, PostgreSQL/RabbitMQ erişimi ve veri dosyası kontrolleri korundu. Eksik bağımlılık/veri dalları kaynak incelemesiyle kontrol edildi; kurulumlar veya gerçek veri dosyaları hata üretmek için değiştirilmedi.

56101/56102/56103/56056 `/health/ready` tamamı 200; Gateway `/` 200 HTML ve `/api/products` 200 JSON. [HTTP kanıtı](../artifacts/local/policy-http.json), [başlatma](../artifacts/local/policy-independent.out.log), [stderr](../artifacts/local/policy-independent.err.log). API kökü 404 başarı ölçütü olarak kullanılmadı. Doğrulanmış arayüz **http://localhost:56056/**; yeni kontrollü oturum çalışır bırakıldı. Durdurmak için Windows PowerShell `-NoProfile -ExecutionPolicy Bypass -File scripts/control-local.ps1 -Action shutdown -ControlDirectory artifacts/local/policy-control` kullanılabilir.

PostgreSQL/RabbitMQ durdurulmadı; SQLite dosyaları ve mesajlar silinmedi/sıfırlanmadı. Önceden belgelenmiş eski Orders mesajı sınırlaması sürer; bu aşama sipariş akışını yeniden test etmez. npm mevcut AngularJS kullanım ömrü ve bağımlılık güvenlik uyarılarını bildirdi; bağımlılıklar bu aşamada değiştirilmedi. Echo Director için PSSecurityException/ExecutionPolicy tanıma önerisi local-windows.md'ye eklendi; Echo Director uygulaması değiştirilmedi.

### Güncel mevcut uygulama incelemesi — 2026-11-04

Onaylanan görev yeniden ele alındığında CMD başlatıcı, PS1 hata mesajları ve yukarıdaki hedefli doğrulama kayıtları zaten mevcuttu; bu uygulama korundu, başarılı testler ve derlemeler tekrarlanmadı. Windows PowerShell salt okunur sorgusu yeniden aynı sonucu verdi: tüm kapsamlar Undefined, etkin politika Restricted. PowerShell 7 terminalindeki RemoteSigned sonucu farklı konağa aittir. Hiçbir politika veya dosya engeli değiştirilmedi.

Mevcut policy-control oturumunun başlatıcı PID 21832 süreci çalışıyor. Yeniden başlatma veya servis durdurma yapılmadan dört /health/ready, Gateway / HTML ve /api/products JSON yanıtları HTTP 200 olarak kontrol edildi. Güncel kanıt: [salt okunur HTTP kaydı](../artifacts/local/policy-current-readonly.json). Geçerli arayüz: http://localhost:56056/ . Önceki başlatıcı parametre/hata çıkışı testlerinin kanıtları yukarıda korunmuştur; yeni test başarısı olarak sunulmaz. Bu incelemede yalnızca bu ek kayıt ve HTTP sonuç dosyası yazıldı; başlatma betiklerinde ek değişiklik gerekmedi.

## 2026-11-04 HTTP delivery migration

Previous results above are historical. Current startup and CI require no broker. Old broker-only messages are not automatically migrated; Sent=false Outbox records retry over HTTP. Databases and broker queues are not reset.

Güncel doğrulama sonucu: `scripts/verify.ps1 -System` başarılı, çıkış kodu 0. Derleme 0 uyarı/0 hata; .NET 38, Web birim/sözleşme 4, masaüstü/mobil E2E 10, gerçek sistem 2 test geçti. Gerçek sistemde Confirmed/Rejected, Inventory kesintisi sırasında Pending, Orders yeniden başlatma, idempotency ve tek stok etkisi doğrulandı. Test sırasında RabbitMQ__Uri erişilemeyen `amqp://127.0.0.1:1/` değerindeydi; broker ile iletişim kurulmadı. Sistem akışı ayrı `artifacts/http-system` SQLite dosyalarında çalıştı. Log: [http-system.log](../artifacts/http-system.log).

Mevcut veriyle gerçek CMD başlatıcısı `-GatewayPort 56056 -CatalogPort 56101 -InventoryPort 56102 -OrdersPort 56103 -ControlDirectory artifacts/local/policy-control` ile başarılı açıldı. İlk CMD denemesinde tek kalan varsayılan bağlantının PowerShell dizisinde açılması hatası bulundu ve düzeltildi; son tekrar başarılıdır. Gateway HTML, ürün API ve dört servisin readiness uçları 200 döndü. [CMD logu](../artifacts/http-cmd.log), [HTTP sonuçları](../artifacts/http-cmd-checks.json). Başlatıcının son derlemesi 0 uyarı/0 hata. Betik sözdizimi kontrolü geçti.

Önceki kontrollü uygulama oturumu DLL kilitlerini bırakması için kendi shutdown kanalıyla kapatıldı; son CMD açılışı aynı port/kontrol dizininde çalışır durumda bırakıldı. Mevcut revision-data dosyaları yerinde, demo seed kapalı; hiçbir veritabanı veya broker kuyruğu silinmedi/sıfırlanmadı. RabbitMQ servisine kontrol komutu gönderilmedi. Geçmiş broker-only mesaj sınırlaması otomatik taşınma kapsamı dışındadır. İlk derlemedeki DLL kilidi uyarıları kontrollü kapanış sonrası giderildi. npm mevcut AngularJS ve bağımlılık uyarılarını bildirdi; bu aşamada bağımlılıklar yükseltilmedi.

## 2026-11-04 - Catalog discovery stage (new verification)

These results apply to this stage, separately from historical records above.

- PostgreSQL integration: `dotnet test tests/Catalog.IntegrationTests --artifacts-path artifacts/catalog-discovery --logger trx` passed (1 test). Covers combined search/currency/price/sort, filtered count and pagination, invalid parameters, and equal-value ordering across pages. Temporary test databases were created and dropped.
- `npx playwright test tests/unit tests/contracts`: 6 passed across desktop/mobile. Optional query parameters and numeric/string types verified.
- `npx playwright test tests/e2e`: 14 passed across 1440x900 and 390x844. Search to detail/cart, Confirmed/Rejected, keyboard entry, no horizontal overflow, empty/error states, filter reset and stale response protection verified. A delayed input update initially interrupted navigation; removing that delay resolved the failure before the final run.
- `playwright.system.config.cjs --grep "gercek arayuz"` (actual Turkish test title): 1 passed, 11.4 seconds. Updated Catalog ran against an isolated migrated PostgreSQL database on port 5202; Gateway on 5204 used existing Inventory/Orders on 56102/56103. Verified search/filter to cart, Confirmed stock decrement, Rejected insufficient stock, desktop/mobile screenshots and overflow. Unrelated outage/restart checks were excluded.
- Web build passed. Normal API build was blocked by existing DLL locks; isolated artifact output compiled successfully. Initial system readiness attempts failed because default ports/connection were absent; final run used explicit ports and an isolated database. Background launch/cleanup command was rejected by automatic approval review without a detailed reason; foreground tool sessions provided the successful run.

Screenshots: `artifacts/system-results`; API TRX: `tests/Catalog.IntegrationTests/TestResults` (2026-11-04 22:40:34). Existing application data was retained; the real system test consumes one unit of the seeded product as asserted by the test.

## 2026-11-10 — Sepet, favoriler ve yenileme sonrası sipariş devamı

Bu aşama mevcut katalog tasarımını koruyarak favori, sepet ve sonuç ekranlarını tamamlar; backend teknolojileri ve servis sözleşmeleri değişmedi. Web kaynakları ve Gateway `wwwroot` çıktıları eşitlendi.

- `npm test`: 10 birim/sözleşme testi geçti (masaüstü ve mobil).
- `npm run test:e2e`: fiyat değişikliği, ürün kaldırılması, yerel depolama engeli, favori/sepet kalıcılığı, adetler, ayrı para birimi özetleri, yanıt kaybı, alınmış sipariş kimliği ve kısmi sonuçları kapsar. Son çalıştırmanın sayısı aşağıda kaydedilmiştir.
- `npm run build` / `node src/Web/build.cjs`: başarılı. Gateway'e kopyalanan app.js/index.html dosyalarının kaynakla SHA256 eşitliği kontrol edildi.
- Yerel başlatıcının çözüm derlemesi: başarılı, 0 uyarı / 0 hata.
- `npx playwright test --config playwright.system.config.cjs`: **3 test geçti, 48.3 saniye**. Gerçek POST yanıtı sunucudan alındıktan sonra tarayıcıya iletilmeden kesildi; yenilemede aynı anahtar tekrarlandı ve aynı sipariş kimliği döndü. İkinci yenilemede yeni POST oluşmadı. Stok quantity ve available değerleri yalnızca bir azaldı; gecikmeli kontrolde aynı kaldı. Normal Confirmed, yetersiz stokta Rejected ve Inventory kesintisinde Pending/Orders yeniden başlatma/servis toparlanması da geçti.

Gerçek servis kanıtları: [JUnit](../artifacts/system.xml), [HTML raporu](../artifacts/system-report/index.html), [ekran görüntüleri](../artifacts/system-results). `persistence-real-response-loss-refresh-and-one-stock-effect` altında cart-desktop/cart-mobile/result-desktop/result-mobile görüntüleri vardır.

Yapılandırılmış görsel kontrol 1440×900 ve 390×844 üzerinde ana sayfa → kategori/arama → detay → sepet → sonuç akışını kapsar. Sepette ürün görseli ve para birimi özeti, sonuçta durum kartı görünürlüğü ve yatay taşma otomatik denetlendi. Her iki boyutta sepet ve sonuç ekran görüntüleri ayrıca açılarak incelendi; kırpılmış kontrol veya yatay taşma görülmedi. Bunlar ekran görüntüsü ve yerleşim kontrolleridir; piksel bazlı baseline karşılaştırması değildir.

İlk gerçek servis denemesinde tarihsel 55432 PostgreSQL adresi erişilemiyordu. Projedeki `artifacts/catalog-enrichment/postgres` yerel örneği 55433 üzerinde başlatıldı. Yeni `shopping_verification` PostgreSQL veritabanı, `artifacts/shopping-system/inventory.db` ve `orders.db` ile ayrılmış gerçek servis verileri kullanıldı; mevcut kullanıcı verileri silinmedi veya sıfırlanmadı. Testler gerçek test stoklarını tüketti. Kontrol dizini `artifacts/local/shopping-system-control`; servis logları `artifacts/local/logs/47b1df6bf45c4b969584330651061496` altındadır.

Depolama kullanılamadığında sayfa belleğinde devam edilir ve yenileme garantisinin olmadığı arayüzde açıkça bildirilir. Sepet tek işlem değildir; her satırın sonucu ayrı korunur. Aynı origin altında yenileme desteklenir; sekmeler arası eşzamanlı düzenleme ve tarayıcılar arası eşitleme bu aşamanın kapsamı değildir.

Son Web tekrarı: **10 birim/sözleşme testi (11.2 sn)** ve **28 masaüstü/mobil E2E testi (29.0 sn)** geçti. Bu tekrar, sepet kontrolünden sonra kaldırılan ürüne POST 404 dönmesinin ürün bazında kesin hata olarak gösterilmesini ve yenileme sonrasında korunmasını da kapsar. Tamamlanan satırlar yeniden gönderilmez; bu durumda Yeni alışveriş kullanılabilir. Son Web build başarılıdır.

Test sonunda `control-local.ps1 -Action shutdown -ControlDirectory artifacts/local/shopping-system-control` komutu **Shutdown verified: 4 application processes exited.** döndürdü. Bu çalışma için başlatılan yerel PostgreSQL örneği de kontrollü kapatıldı. Ayrılmış test verileri ve raporlar inceleme için korundu. Önceki kullanıcı uygulama oturumlarına müdahale edilmedi.

## 2026-11-10 — PostgreSQL baglantisi ve VS Code baslaticisi

Bu asama yalnizca yerel baslatma sorununu ele alir. Once salt okunur kontroller yapildi: global.json ile SDK 10.0.403 uyumlu; Node 24.14.1/npm 11.12.1 erisilebilir. PostgreSQL Windows servisi, PostgreSQL sureci ve 5432/55432/55433 dinleyicisi yoktu. ConnectionStrings__Catalog Process/User/Machine kapsamlarinda ayarsizdi. Mevcut PostgreSQL 18 veri dizininin postmaster.opts dosyasi 127.0.0.1:55433 gosteriyordu. Mevcut ornek pg_ctl ile baslatildi; postgres ve shopping_verification veritabanlari salt okunur sorguyla goruldu. Yeni veritabani veya kullanici olusturulmadi.

Eski 55432/project1_revision varsayimi kaldirildi. Verilen Catalog baglantisi korunur; yoksa acik ayar yonlendirmesi gelir. TCP hatasi guvenli host/port gosterir. TCP sonrasi Catalog kapanisinda kimlik dogrulama ve yetki kodlari ayrilir; diger veritabani/migration hatalari log yoluyla bildirilir. Tam baglanti/parola yazdirilmaz. Mevcut SQLite Data Source ayarlari korunur ve ReadWrite zorunludur. VS Code gorevi ve terminal ayni CMD baslaticiyi kullanir; kalici politika degistirilmedi.

Proje kokunde kullanilan komut:

```powershell
$env:ConnectionStrings__Catalog = 'Host=127.0.0.1;Port=55433;Database=shopping_verification;Username=postgres;Timeout=5'
.\scripts\start-existing-local.cmd -ControlDirectory "artifacts/local/connection-control"
```

- Gercek CMD baslatici Web ve cozum derlemesini tamamladi: 0 uyari/0 hata.
- Dort /health/ready HTTP 200; Gateway / HTTP 200 text/html; /api/products HTTP 200 application/json, JSON parse basarili. Kanit: artifacts/local/connection-http.json.
- scripts/test-local-launcher.ps1: eksik baglanti, erisilemeyen PostgreSQL (hata metninde hedef adres/port), dolu port ve eksik mevcut SQLite dosyasi kontrollerinin tamami PASS. Deneme parolasi ciktiya sizmadi. Dolu porttaki test dinleyicisi korunarak kontrol edildi. Kayit: artifacts/local/connection-failure-checks.log. Testleri uygulama portlari bosken calistirin.
- Bosluklu artifacts/local/connection launcher checks yoluna kopyalanan gercek baslaticilar farkli calisma dizininden cagrildi; eksik baglanti hatasi ve hata cikis kodu dogrulandi. PowerShell sozdizimi kontrolleri gecti.
- Sistem testlerinin kullandigi control-local.ps1 restart inventory,orders cagrisi gecti; ardindan dort readiness yeniden 200. Mevcut shop.spec.cjs, LOCAL_CONTROL_DIRECTORY ve varsayilan port tuketimi degistirilmedi. Siparis/stok tuketen tam sistem test paketi bu baslatma asamasinda tekrarlanmadi.
- Kontrol kanali shutdown iki kez `Shutdown verified: 4 application processes exited.` verdi; ilk kapanistan sonra uygulama portlarinda dinleyici olmadigi kontrol edildi. Sonrasinda ayni komutla tekrar baslatildi.

Son durumda uygulama http://localhost:5100/ adresinde acik birakildi; kontrol dizini artifacts/local/connection-control. Baslatma kayitlari bu dizindeki launcher.out.log ve launcher.err.log dosyalarindadir. Kapatma:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/control-local.ps1 -Action shutdown -ControlDirectory "artifacts/local/connection-control"
```

Mevcut revision-data SQLite dosyalari ve PostgreSQL veri dizini silinmedi/sifirlanmadi. Ilgisiz surecler durdurulmadi. PostgreSQL yerel ornegi uygulamanin yeniden acilabilmesi icin calisir birakildi. npm mevcut AngularJS kullanim omru ve iki guvenlik bulgusu (1 moderate, 1 high) bildirdi; bagimlilik surumleri degistirilmedi.

### 2026-11-10 — Güncel bağlantı ve başlatıcı tekrar kontrolü

Salt okunur incelemede SDK 10.0.403, Node 24.14.1, npm.cmd 11.12.1 doğrulandı. Windows PostgreSQL servisi yok; mevcut pg_ctl örneği 127.0.0.1:55433 üzerinde çalışıyor. psql salt okunur sorgusu shopping_verification veritabanını ve gerçek sunucu adres/portunu doğruladı. Catalog ortam değişkeni Process/User/Machine kapsamında ayarsızdı; hiçbir kalıcı ortam ayarı veya yürütme politikası değiştirilmedi. Düz npm komutu Restricted politikaya takılıyor; başlatıcı npm.cmd kullanıyor.

Mevcut düzeltmeler korundu. Catalog süreç hatasına güvenli hedef adres/port eklendi; VS Code görevinin kontrol dizini terminal komutuyla eşitlendi. Yerel kılavuzdaki çelişkili SQLite varsayılanı ve doğrudan PS1 başlatma örnekleri düzeltildi.

Kullanılan komut (proje kökünde):

```powershell
$env:ConnectionStrings__Catalog = 'Host=127.0.0.1;Port=55433;Database=shopping_verification;Username=postgres;Timeout=5'
.\scripts\start-existing-local.cmd -ControlDirectory "artifacts/local/connection-control"
```

Mevcut kontrollü oturum shutdown ile kapatıldı: `Shutdown verified: 4 application processes exited.` Ardından gerçek CMD başlatıcı yeniden çalıştırıldı; Web ve çözüm derlemesi başarılı, 0 uyarı/0 hata. Güncel başlatma kayıtları: artifacts/local/connection-control/recheck.out.log ve recheck.err.log.

- test-local-launcher.ps1: eksik bağlantı, erişilemeyen PostgreSQL, dolu port ve eksik mevcut veri dosyası kontrolleri PASS; deneme parolası çıktıya sızmadı, ilgisiz dinleyici korundu.
- Gerçek başlatıcıların boşluk içeren kopyası farklı çalışma dizininden çağrıldı; eksik bağlantı hatası ve başarısız çıkış kodu doğrulandı.
- Dört ilgili PS1 sözdizimi ve VS Code görev JSON kontrolü geçti.
- Sistem testindeki kontrol tüketimiyle aynı `restart -Services inventory,orders` çağrısı başarılı. Sonrasında dört readiness HTTP 200, Gateway / HTTP 200 text/html ve /api/products HTTP 200 application/json; JSON ayrıştırması başarılı. Kanıt: artifacts/local/connection-recheck-http.json.

Tam sipariş/stok test paketi bu başlatıcı aşamasında tekrarlanmadı. SQLite dosyaları ve PostgreSQL verileri silinmedi veya sıfırlanmadı; ilgisiz süreçler kapatılmadı. Uygulama http://localhost:5100/ adresinde çalışır bırakıldı. Kontrol dizini artifacts/local/connection-control; kılavuzdaki shutdown komutu bu oturumu kapatır. npm mevcut bağımlılık uyarılarını bildirdi; bağımlılık sürümleri değiştirilmedi.
## 2026-11-10 — Son başlatıcı kabul kontrolü

Bu kontrolde önce salt okunur SDK/Node/npm, Windows servisi, dinleyici ve veri dosyası incelemesi yapıldı. SDK 10.0.403, Node 24.14.1, npm.cmd 11.12.1; PostgreSQL Windows servisi yok. Çalışan yerel PostgreSQL örneği SQL `current_database(), inet_server_addr(), inet_server_port()` sorgusuyla `shopping_verification`, `127.0.0.1:55433` olarak doğrulandı. 55432 varsayılmadı. Kalıcı ortam veya yürütme politikası değiştirilmedi.

Önceki uygulama kendi kontrol kanalından kapatıldı: `Shutdown verified: 4 application processes exited.` Gerçek CMD başlatıcısı aşağıdaki bağlantı ve komutla yeniden açıldı:

```powershell
$env:ConnectionStrings__Catalog = 'Host=127.0.0.1;Port=55433;Database=shopping_verification;Username=postgres;Timeout=5'
.\scripts\start-existing-local.cmd -ControlDirectory "artifacts/local/connection-control"
```

Web ve çözüm derlemesi başarılı (0 uyarı, 0 hata). Sistem testinin kullandığı `restart -Services inventory,orders` kontrol çağrısı başarılı. Ardından dört servisin readiness uçları HTTP 200; Gateway `/` HTTP 200 text/html, `/api/products` HTTP 200 application/json ve JSON ayrıştırması başarılı. Kanıt: [HTTP sonuçları](../artifacts/local/connection-acceptance-http.json). Başlatma kayıtları: `artifacts/local/connection-control/acceptance.out.log` ve `acceptance.err.log`.

`test-local-launcher.ps1`: eksik bağlantı, erişilemeyen PostgreSQL, geçersiz port, dolu uygulama portu ve eksik mevcut SQLite dosyası PASS. Deneme parolası çıktıda görülmedi; dolu porttaki ilgisiz dinleyici korundu. Gerçek başlatıcıların boşluklu yoldaki kopyası farklı çalışma dizininden çağrıldı; beklenen eksik bağlantı hatası ve başarısız çıkış kodu doğrulandı. Dört ilgili PS1 sözdizimi ve VS Code görev JSON kontrolü geçti. Catalog readiness zaman aşımı da artık güvenli hedef adres/port ve ilk TCP kontrolünün geçtiği bilgisini verir; bu zaman aşımı dalı kaynak incelemesiyle kontrol edildi.

Uygulama **http://localhost:5100/** üzerinde çalışır bırakıldı. Kapatma:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/control-local.ps1 -Action shutdown -ControlDirectory "artifacts/local/connection-control"
```

Mevcut SQLite/PostgreSQL verileri silinmedi veya sıfırlanmadı. Sipariş/stok tüketen tam sistem paketi bu başlatıcı aşamasında yeniden çalıştırılmadı; kontrol kanalı uyumu gerçek servislerle denetlendi. npm mevcut bağımlılık uyarılarını bildirdi; bağımlılık sürümleri değiştirilmedi.


### Başarılı yerel başlatma kaydının korunması — 2026-11-10

Bu aşamada mevcut başlatıcılar, VS Code görevi, yukarıdaki son kabul kaydı ve connection-acceptance-http.json salt okunur incelendi. Kanıt dosyasında Gateway/Catalog/Inventory/Orders readiness yanıtlarının dördü de 200; Gateway HTML ve ürün JSON yanıtları da 200 olarak kayıtlıdır. Bunlar mevcut kabul sonuçlarıdır, yeni bir çalışma sonucu değildir.

Catalog bağlantısı mevcut komutta açıkça 127.0.0.1:55433 olarak ayarlanır; başlatıcı bağlantı eksikse durur, tarihsel 55432 profilini otomatik kullanmaz. Mevcut CMD komutu ve veri koruma davranışı korundu; yeni geliştirme gerekmedi. Yalnızca kullanım kılavuzuna mevcut komut ve kabul kaydı yönlendirmesi eklendi. Başlatma, derleme, test ve görsel kontroller tekrarlanmadı; PostgreSQL/SQLite verilerine veya çalışan süreçlere müdahale edilmedi.
