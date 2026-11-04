# Page snapshot

```yaml
- generic [active] [ref=e1]:
  - link "İçeriğe geç" [ref=e2] [cursor=pointer]:
    - /url: "#main"
  - banner [ref=e3]:
    - link "Mini Mağaza" [ref=e4] [cursor=pointer]:
      - /url: /
    - navigation "Ana menü" [ref=e5]:
      - button "Ürünler" [ref=e6] [cursor=pointer]
      - button "Sepet (1)" [ref=e7] [cursor=pointer]
      - button "Sipariş sonucu" [ref=e8] [cursor=pointer]
  - main [ref=e9]:
    - paragraph [ref=e10]: KATALOG / ALIŞVERİŞ
    - status [ref=e11]: Ürün sepete eklendi.
    - generic [ref=e12]:
      - heading "Sipariş sonucu" [level=1] [ref=e13]
      - article [ref=e14]:
        - heading "Klavye" [level=2] [ref=e15]
        - generic [ref=e16]:
          - paragraph [ref=e17]: "Sipariş: 5c359c67-e258-4a0d-b61d-45bebf6b3876"
          - paragraph [ref=e18]: "Son tutar: ₺750,00"
          - paragraph [ref=e19]: Stok onayı bekleniyor.
      - button "Durumu yeniden sorgula" [ref=e20] [cursor=pointer]
      - button "Yeni alışveriş" [disabled] [ref=e21]
  - contentinfo [ref=e22]: Mini Mağaza · Güvenli stok rezervasyonu
```