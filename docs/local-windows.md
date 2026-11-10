# Windows üzerinde Docker olmadan çalıştırma

> Güncel kullanım: Bu makine için açık Catalog bağlantısı ve mevcut CMD komutu aşağıdaki **2026-11-10 — VS Code terminalinden baslatma** bölümündedir. Çalışan oturum için http://localhost:5100/ adresini kullanın. Belgelenmiş başarılı sonuç [son başlatıcı kabul kaydında](verification.md#2026-11-10--son-başlatıcı-kabul-kontrolü), altı HTTP 200 yanıtı [mevcut kanıt dosyasında](../artifacts/local/connection-acceptance-http.json) korunur. Bu sonuçları yeniden elde etmek için test veya görsel kontrol çalıştırmak gerekmez; tarihsel 55432 adresini kullanmayın.


PostgreSQL yönetim aracında uygulamaya özel kullanıcı ve veritabanı oluşturun (parolayı değiştirin):

```sql
CREATE ROLE catalog LOGIN PASSWORD 'YOUR_LOCAL_PASSWORD';
CREATE DATABASE catalog OWNER catalog;
```


Proje kökünde PowerShell:

```powershell
$env:ConnectionStrings__Catalog = 'Host=localhost;Port=5432;Database=catalog;Username=catalog;Password=YOUR_LOCAL_PASSWORD;Timeout=5'
.\scripts\start-existing-local.cmd
```


Arayüz http://localhost:5100, Catalog 5101, Inventory 5102, Orders 5103 portundadır. -GatewayPort, -CatalogPort, -InventoryPort ve -OrdersPort ile portları değiştirebilirsiniz; Gateway ve Orders hedefleri aynı değerlere otomatik ayarlanır. Her servis /health/live ve /health/ready sunar. -StartupTimeoutSeconds (varsayılan 90) servis başına beklemeyi belirler.

Mevcut veri başlatıcısının varsayılan SQLite dosyaları artifacts/revision-data/inventory.db ve orders.db dosyalarıdır; eksiklerse başlatma durur. Yalnızca start-local.ps1 ile yeni veri başlatıldığında varsayılan dizin artifacts/local/data olur. ConnectionStrings__Inventory ve ConnectionStrings__Orders ile ayrı kalıcı dosyalar seçilebilir; mutlak dosya yolları ve mevcut, yazılabilir üst dizin kullanın. Önceden farklı çalışma dizininde oluşturulan .db dosyalarını kullanmak için bu değişkenleri eski dosyaların mutlak yollarına ayarlayın. Betik veri silmez.


Yerel PostgreSQL üzerinde geçici veritabanı oluşturup silebilen test hesabını ayarlayın:

```powershell
$env:CATALOG_TEST_POSTGRES = 'Host=localhost;Database=postgres;Username=postgres;Password=YOUR_LOCAL_PASSWORD'
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
# Normal başlatıcı kapalıyken ve uygulama bağlantıları ayarlanmış olarak:
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -System
```


CI aynı betiği ayrılmış yerel Windows runner üzerinde çalıştırır; hazırlık [yerel kılavuzda](local-windows.md). Raporlar, ekran görüntüleri ve servis logları `artifacts` altında, CI'da `verification-results` artifact'ında saklanır. Paylaşmadan önce loglardaki bağlantı ayrıntılarını inceleyin.

## CI için yerel runner




## Mevcut yerel verilerle devam etme (2026-11-04)

Proje k?k?nden tek komut:

```powershell
.\scripts\start-existing-local.cmd -ControlDirectory "artifacts/local/connection-control"
```

Existing data: artifacts/revision-data/inventory.db and orders.db are opened with Mode=ReadWrite; missing files stop startup. Demo seeding is disabled. Set ConnectionStrings__Catalog explicitly; no PostgreSQL profile is assumed. Existing Catalog environment settings are preserved. Files, WAL/SHM and PostgreSQL data are never reset.

Aray?z: http://localhost:5100 ; ?r?nler: http://localhost:5100/api/products ; stok: http://localhost:5102/stock/11111111-1111-1111-1111-111111111111 . API k?k?nde `/` i?in 404, tan?ml? u? noktalar?n yerine kullan?lmamal?d?r. Durdurmak i?in terminalde Ctrl+C kullan?n.


## 2026-11-04 — Önizleme portuyla kontrollü yeniden başlatma

Proje kökünde:

```powershell
.\scripts\start-existing-local.cmd -GatewayPort 56056 -ControlDirectory "artifacts/local/existing-control"
```

Gateway adresi http://127.0.0.1:56056/ olur. GatewayPort alt başlatıcıya aktarılır; varsayılan 5100 korunur. Aynı kontrol dizininde eski oturum varsa önce shutdown istenir; uygulama süreçlerinin çıkışı ve ready işaretinin kaldırılması beklenir, ardından derleme başlar. Süre aşımında derleme yapılmaz. Kontrol kanalı olmayan dolu portlarda başlatma durur; genel süreç öldürme yapılmaz.

Yalnızca kapatmak için:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/control-local.ps1 -Action shutdown -ControlDirectory artifacts/local/existing-control
```


### Teslim veya bağımsız çözüm derlemesi öncesi

Çalışan uygulamaları aynı kontrol kanalı üzerinden kapatın ve komutun başarıyla tamamlanmasını bekleyin; kapanış hatası varsa derlemeye geçmeyin:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/control-local.ps1 -Action shutdown -ControlDirectory artifacts/local/existing-control
if ($LASTEXITCODE -ne 0) { throw 'Shutdown failed; rebuild blocked.' }
dotnet build Project1.sln
$buildExitCode = $LASTEXITCODE
Write-Host "Build exit code: $buildExitCode"
```


## 2026-11-04 — Geçici yürütme politikasıyla mevcut profili başlatma

İncelenmiş proje dosyaları için proje kökünde önerilen komut:

```powershell
.\scripts\start-existing-local.cmd -GatewayPort 56056 -CatalogPort 56101 -InventoryPort 56102 -OrdersPort 56103 -ControlDirectory "artifacts/local/policy-control"
```


Başlatıcı yalnızca kendi yanındaki `start-existing-local.ps1` dosyasını mutlak ve tırnaklı yolla, Windows PowerShell `-NoProfile -ExecutionPolicy Bypass -File` ile çalıştırır. `Bypass` yalnızca çocuk işlemine aittir; kalıcı politika veya Zone.Identifier değiştirilmez. Başlatıcı dosya imzası denetleyicisi değildir: yalnızca kaynağını incelediğiniz güvenilir proje kopyasında kullanın. `MachinePolicy`/`UserPolicy` kurumsal kuralları önceliklidir; engel sürerse kurum yöneticisine başvurun. `Set-ExecutionPolicy` veya `Unblock-File` çalıştırmayın.


Her uygulamada `/health/ready`, Gateway üzerinde `/` HTML ve `/api/products` JSON kontrol edilir. Başarı mesajındaki seçilen port gerçek arayüz adresidir; servis API kökündeki 404 tek başına hata değildir. Windows PowerShell modüllerinin PowerShell 7 ortamındaki yollarla karışmaması için yalnızca çocuk işlemin PSModulePath değeri Windows PowerShell modül yollarına ayarlanır.

Echo Director önerisi (bu projede uygulanmadı): hata analizinde `PSSecurityException`, `UnauthorizedAccess`, `running scripts is disabled` ve `ExecutionPolicy` metinlerini birlikte değerlendirin; önce salt okunur politika kapsamlarını sorgulayın, işlem kapsamlı başlatıcı önerin ve kurumsal politika üstünlüğünü belirtin. API kökündeki 404 yerine yapılandırılmış arayüz ve sağlık uçlarını kontrol edin.


Current HTTP delivery configuration: .NET 10 SDK (global.json), Node.js 24/npm and PostgreSQL 17+ are required. SQLite needs no service. Set ConnectionStrings__Catalog to your PostgreSQL connection; retain schema migration permissions. Run scripts/start-local.ps1 or scripts/start-existing-local.cmd for the existing data profile. SDK/Node, PostgreSQL reachability, occupied ports and existing SQLite file checks remain enabled.

Orders uses Inventory__Url (default http://localhost:5102); Inventory uses Orders__Url (default http://localhost:5103). The launcher supplies the selected ports. Workers POST persisted Outbox payloads to /internal/messages with a 5 second timeout and retry every second. Sent is written only after successful consumer completion. Inbox and persistent order decisions prevent a second stock effect, including lost HTTP responses and process restarts. Unsupported event versions receive 400 and remain pending for operator correction. Internal endpoints must stay on the trusted service network; Gateway does not publish them.

HTTP teslimatı için .NET 10 SDK (global.json), Node.js 24/npm ve PostgreSQL 17+ gerekir. SQLite ayrı servis gerektirmez. `ConnectionStrings__Catalog` bağlantısını ve migration yetkilerini ayarlayın. `scripts/start-local.ps1` veya mevcut veriler için `scripts/start-existing-local.cmd` çalıştırılır. SDK/Node, PostgreSQL, port ve mevcut SQLite dosyası kontrolleri korunur. Varsayılan arayüz http://localhost:5100 adresindedir; Ctrl+C uygulama süreçlerini kapatır.

Orders, `Inventory__Url` (varsayılan http://localhost:5102); Inventory, `Orders__Url` (varsayılan http://localhost:5103) kullanır. Başlatıcı seçilen portları aktarır. Worker kalıcı Outbox içeriğini `/internal/messages` ucuna POST eder; HTTP zaman aşımı 5 saniye, yeniden deneme aralığı 1 saniyedir. Sent yalnızca tüketici işlemi tamamlanıp başarılı yanıt alındıktan sonra yazılır. Inbox ve kalıcı sipariş kararları tekrar teslimatta, yanıt kaybında ve süreç yeniden başladığında ikinci stok etkisini engeller. Desteklenmeyen olay/sürüm 400 alır ve operatör sözleşmeyi düzeltene kadar bekler. İç uçlar güvenilir servis ağında tutulmalıdır; Gateway bunları yayınlamaz.

RabbitMQ artık gerekli değildir. Servisine ve eski kuyruklarına dokunulmaz. Yalnızca eski broker kuyruğunda kalan mesajlar otomatik taşınmaz. Sent=false Outbox kayıtları HTTP ile gönderilir; Sent=true kayıtlar yeniden açılmaz. SQLite ve PostgreSQL verileri korunur. `scripts/control-local.ps1` inventory/orders yeniden başlatma ve shutdown işlemlerini destekler. CI yalnızca `CATALOG_TEST_POSTGRES` ve `CATALOG_CONNECTION_STRING` bağlantı sırlarını gerektirir; broker CLI veya URI/vhost ayarı gerekmez. Sistem kontrolleri Inventory kesintisini ve Orders yeniden başlatmasını kapsar.

## 2026-11-10 — VS Code terminalinden baslatma

Once salt okunur kontroller: `dotnet --version`, `node --version`, `npm.cmd --version`, `Get-Service *postgres*`, `Get-NetTCPConnection -State Listen`.
Bu bilgisayarda SDK 10.0.403, Node 24.14.1/npm 11.12.1 bulundu. PostgreSQL Windows servisi ve dinleyen PostgreSQL portu yoktu. Mevcut artifacts/catalog-enrichment/postgres/postmaster.opts dosyasi PostgreSQL 18 ile 127.0.0.1:55433 adresini gosteriyordu. Mevcut ornek pg_ctl ile acildi; shopping_verification veritabani salt okunur sorguyla dogrulandi. Veri dizini ve kimlik dogrulama ayarlari degistirilmedi.

Bu bilgisayarda ornek kapaliysa proje kokunden:

```powershell
& 'C:/Program Files/PostgreSQL/18/bin/pg_ctl.exe' -D "$PWD/artifacts/catalog-enrichment/postgres" -l "$PWD/artifacts/catalog-enrichment/postgres.log" -o '-p 55433 -h 127.0.0.1' start
```

VS Code PowerShell terminalinde proje kokunden:

```powershell
$env:ConnectionStrings__Catalog = 'Host=127.0.0.1;Port=55433;Database=shopping_verification;Username=postgres;Timeout=5'
.\scripts\start-existing-local.cmd -ControlDirectory "artifacts/local/connection-control"
```

Bu adres yalnizca bu makinede dogrulandi; baska makinede kendi PostgreSQL adresinizi ve kimlik bilgilerinizi ayarlayin. Parolayi gorev dosyasina veya belgelere yazmayin. Baslatici verilen baglantiyi korur; eksikse ayar ister, 55432 profilini eklemez.

Basari mesajindan sonra http://localhost:5100/ arayuzunu acin; JSON: http://localhost:5100/api/products . Terminal acik kalmalidir. Ctrl+C veya baska terminalden kontrol kanali uygulamalari kapatir:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/control-local.ps1 -Action shutdown -ControlDirectory "artifacts/local/connection-control"
```

VS Code gorevi ayni CMD baslaticiyi proje kokunde calistirir. Gorev VS Code isleminin ortamini devralir; terminalde ayarlanan $env degeri onceden acik VS Code islemine aktarilmaz. Gorev kullanilacaksa baglantiyi VS Code'u baslatan ortamda ayarlayip VS Code'u o ortamdan acin. Terminal komutu dogrudan kullanilabilir. Bosluklu yollarda `& "D:/proje yolu/scripts/start-existing-local.cmd"` kullanin. Kalici yurutme politikasi degismez.

TCP hatasi guvenli hedef adresini ve portunu gosterir. Port 1-65535 arasinda bir tamsayi olmalidir; gecersiz deger hata ciktisina yazilmaz. TCP gectigi halde Catalog kapanirsa kimlik dogrulama/yetki veya migration hatasini servis loglarinda inceleyin. Catalog readiness zaman asimi da hedef adres/portu ve ilk TCP kontrolunun gectigini belirtir. Dolu portta ilgisiz surecler kapatilmaz. Mevcut SQLite dosyalari ReadWrite ile acilir; eksik dosya olusturulmaz. Inventory/Orders icin verilen Data Source yollari korunur.

Hedefli hata kontrolleri (erisilebilir Catalog baglantisi ayarli olmali):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-local-launcher.ps1
```

Not: Hata testlerini calistirmadan once uygulamayi kendi kontrol kanaliyla kapatin; test portlari bos olmalidir. Son dogrulanan oturum zaten calisiyorsa once http://localhost:5100/ adresini kullanabilirsiniz.
