# Alışveriş arayüzü

[Yerel Windows kılavuzunu](local-windows.md) izleyerek uygulamayı başlatın. Varsayılan arayüz http://localhost:5100 adresindedir.

Ana sayfadan kategori veya aramayla ürün bulun, ayrıntılarını inceleyin ve sepete ekleyin. Katalog ve ürün detayındaki favori düğmeleriyle seçimleri saklayabilirsiniz. Favoriler ekranından ürün detayına veya doğrudan sepete geçilebilir; favorilerden çıkarmak sepeti değiştirmez.

Sepette ürün görseli, adet alanı, artırma/azaltma ve kaldırma kontrolleri bulunur. Sepet açıldığında güncel ürün bilgileri katalogdan okunur. Fiyat değişikliği bildirilir; kaldırılmış ürünü sepetten çıkarmak gerekir. Geçici bağlantı hatasında fiyat kontrolünü yeniden deneyin. Tutarlar TRY/USD/EUR için ayrı gösterilir; döviz dönüşümü veya ortak toplam yapılmaz. Katalog fiyatları tahminidir; kesin birim fiyat ve para birimi sipariş yanıtından alınır.

Mevcut API her ürün için ayrı sipariş oluşturur; sepet atomik değildir. Bir ürün onaylanırken diğeri reddedilebilir veya bekleyebilir. Sonuç kartlarında ürün, adet, sipariş kimliği, kesin tutar ve durum gösterilir:

- **Stok onayı bekleniyor (Pending):** durum otomatik sorgulanır.
- **Onaylandı (Confirmed):** bu satır tekrar gönderilmez.
- **Reddedildi (Rejected):** stok yetersizliği veya rezervasyon reddi; bu sonuç aynı işlemle yeniden sipariş oluşturmaz.
- **Yanıt bekleniyor:** Durumu yeniden sorgula düğmesi aynı işlem anahtarıyla tekrar dener.

Favoriler, sepet, adetler ve devam eden sipariş aynı tarayıcı/aynı origin altında `vera.shopping.v1` yerel depolama kaydında korunur. İşlem anahtarı gönderimden önce, sipariş kimliği alındığında hemen kaydedilir. Yenileme sonrası devam eden işlem otomatik sürer: kimlik varsa GET ile sorgulanır; yanıt kaybolmuşsa aynı anahtarla POST yinelenir. API'nin mevcut idempotency davranışı aynı sipariş kimliğini döndürür ve stok ikinci kez düşmez. İşlem sırasında sepet kilitlenir. Tüm satırlar sonuçlandıktan sonra Yeni alışveriş düğmesi yeni sepet açar.

Yerel depolama engelliyse veya doluysa uyarı gösterilir ve alışveriş sayfa belleğinde sürer. Bu durumda yenileme sonrası devam garantisi yoktur. Tarayıcı verilerini silmek, özel oturumu kapatmak veya farklı origin kullanmak kaydı taşımayacaktır. Bu aşama tarayıcılar arası veya eşzamanlı sekmeler arası sepet eşitlemesi içermez.

## Doğrulama

`src/Web` içinde:

```sh
npm test
npm run test:e2e
npm run build
```

Birim/sözleşme testleri gerçek AngularJS istemcilerini ve OpenAPI sözleşmesini kontrol eder. E2E testleri kontrollü API yanıtlarıyla çalışır. Masaüstü 1440×900 ve mobil 390×844 projeleri; favori/sepet kalıcılığı, ayrı döviz özetleri, fiyat değişikliği, kaldırılmış ürün, depolama engeli, yanıt kaybı, yenileme ve kısmi sonuçları kapsar. Görsel senaryo ana sayfa → kategori/arama → detay → sepet → sonuç akışını ekran görüntüleri, görsel görünürlüğü ve yatay taşma kontrolleriyle doğrular. Görseller `src/Web/test-results` altındadır.

Gerçek servis testleri için kontrollü yerel sistemi başlatıp `LOCAL_CONTROL_DIRECTORY` değerini kontrol dizinine ayarlayın ve `npx playwright test --config playwright.system.config.cjs` çalıştırın. `SHOP_URL` ve `INVENTORY_URL` isteğe bağlı port adresleridir. Testler gerçek sipariş oluşturur ve test stoklarını tüketir; ayrılmış veritabanları kullanın. Servis kesintisi testleri yalnızca kontrol kanalının yönettiği uygulamaları durdurup yeniden açar. Bu aşamanın gerçek sonuçları [doğrulama kaydındadır](verification.md).
