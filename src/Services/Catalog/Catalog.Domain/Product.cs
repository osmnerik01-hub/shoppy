namespace Catalog.Domain;

public sealed class Product
{
    private Product() { }
    public Product(string name, string description, decimal price, string currency)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new ArgumentException("Name must contain 1–200 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 4000) throw new ArgumentException("Description must contain 1–4000 characters.", nameof(description));
        if (price <= 0 || price > 9999999999.99m || decimal.Round(price, 2) != price) throw new ArgumentException("Price must be positive with at most two decimal places.", nameof(price));
        if (currency is not ("TRY" or "USD" or "EUR")) throw new ArgumentException("Supported currencies: TRY, USD, EUR.", nameof(currency));
        Name = name.Trim(); Description = description.Trim(); Price = price; Currency = currency;
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "";
}
