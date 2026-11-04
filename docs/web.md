# Alışveriş arayüzü

[Yerel Windows kılavuzunu](local-windows.md) izleyip `./scripts/start-local.ps1` çalıştırın. Betik Web bağımlılıklarını yükler ve tüm uygulamaları derleyip başlatır. Arayüz http://localhost:5100 adresindedir. Ctrl+C ile durdurun.


Ürün ayrıntısından sepete ekleyin, etiketli adet alanını değiştirin ve sipariş verin. Mevcut API her ürün için ayrı sipariş oluşturur; sepet atomik değildir. Gösterilen sepet fiyatı tahminidir. Son tutar ve Pending/Confirmed/Rejected durumu sipariş API'sinden okunur. Stok rezervasyonu mevcut servis mesajlaşmasıyla gerçekleşir. Yanıt kaybında aynı işlem anahtarıyla yeniden deneme yapılır; işlem bitene kadar sepet kilitlenir. Sayfayı yenilemek bellekteki sepeti ve işlem anahtarını siler; belirsiz bir işlemde sayfayı yenilemeden durum düğmesini kullanın.

## Doğrulama

`src/Web` içinde:

```sh
npm ci
npm test
npm run test:e2e
npm run build
```

Testler kurulu Chrome kullanır. Birim testleri gerçek AngularJS istemci fabrikalarını yalıtılmış HTTP bağımlılığıyla sınar. Tüketici testleri gerçek istemcilerin HTTP isteklerini sürümlü OpenAPI belgelerine karşı kontrol eder. Tarayıcı testleri açıkça tanımlanmış API test yanıtlarıyla başarılı alışverişi, sunucu fiyatını, stok reddini, boş listeyi, bağlantı hatasını, adet doğrulamasını ve yinelenen gönderim engelini sınar; canlı servis entegrasyon testi değildir. Masaüstü 1440×900 ve mobil 390×844 ekran görüntüleri `src/Web/test-results` altına yazılır. Klavye içerik bağlantısı ve adet etiketi de denetlenir. Sunucu stok tutarlılığı testleri mevcut Inventory/Orders entegrasyon testlerindedir.
