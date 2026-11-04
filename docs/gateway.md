# Gateway ve gözlemlenebilirlik

[Windows yerel kurulum](local-windows.md) tamamlandıktan sonra `./scripts/start-local.ps1` çalıştırın. YARP http://localhost:5100 üzerinde Web arayüzünü sunar; Catalog localhost:5101 ve Orders localhost:5103 hedeflerine yönlendirir. Inventory localhost:5102 stok doğrulaması/yönetimi içindir. Ctrl+C uygulamaları durdurur.

JSON loglar `artifacts/local/logs` altında servis başına stdout/stderr dosyalarında tutulur. X-Correlation-ID en fazla 64 ASCII harf/rakam/tire/alt çizgi kabul eder; aksi durumda yeni kimlik üretilir. Kimlik HTTP boyunca aktarılır. W3C traceparent ve OpenTelemetry ASP.NET Core/HttpClient izleri kullanılır.

Collector temel çalışma için gerekli değildir. OTLP dışa aktarımı yalnızca `OTEL_EXPORTER_OTLP_ENDPOINT` ayarlandığında açılır. İsteğe bağlı olarak OpenTelemetry Collector Contrib Windows yürütülebilir dosyasını kurup ayrı terminalde çalıştırın (yolu kendi kurulumunuza uyarlayın):

```powershell
& 'C:/Tools/otelcol-contrib.exe' --config ./deploy/observability/collector.yaml
```

Yapılandırma yalnızca 127.0.0.1:4317 üzerinde OTLP/gRPC dinler ve izleri collector terminaline yazar. Uygulamaları başlatmadan önce `$env:OTEL_EXPORTER_OTLP_ENDPOINT = 'http://localhost:4317'` ayarlayın. Collector terminalinde Ctrl+C ile durdurun; kullanmayacaksanız ortam değişkenini kaldırın. Yerel loglar collector olmadan da tutulur.

Her serviste `/health/live` ve `/health/ready` bulunur. Başlatıcı readiness bekler. `./scripts/verify.ps1 -System` gerçek alışveriş ve kesinti kontrollerini çalıştırır.
