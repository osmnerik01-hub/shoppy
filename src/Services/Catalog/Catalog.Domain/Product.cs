namespace Catalog.Domain;

public sealed class Product
{
    private Product() { }
    public Product(string name, string description, decimal price, string currency, string? category = null, string? brand = null, IEnumerable<ProductImage>? images = null, IEnumerable<ProductSpecification>? specifications = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new ArgumentException("Name must contain 1–200 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 4000) throw new ArgumentException("Description must contain 1–4000 characters.", nameof(description));
        if (price <= 0 || price > 9999999999.99m || decimal.Round(price, 2) != price) throw new ArgumentException("Price must be positive with at most two decimal places.", nameof(price));
        if (currency is not ("TRY" or "USD" or "EUR")) throw new ArgumentException("Supported currencies: TRY, USD, EUR.", nameof(currency));
        Category = OptionalLabel(category, nameof(category));
        Brand = OptionalLabel(brand, nameof(brand));
        var gallery = (images ?? []).ToArray();
        if (gallery.Length > 20 || gallery.Any(x => x is null) || gallery.Select(x => x.Position).Distinct().Count() != gallery.Length)
            throw new ArgumentException("Images require unique positions and at most 20 entries.", nameof(images));
        Images = Array.AsReadOnly(gallery.OrderBy(x => x.Position).ToArray());
        var specs = (specifications ?? []).ToArray();
        if (specs.Length > 100 || specs.Any(x => x is null) || specs.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != specs.Length)
            throw new ArgumentException("Specifications require unique names and at most 100 entries.", nameof(specifications));
        Specifications = Array.AsReadOnly(specs);
        Name = name.Trim(); Description = description.Trim(); Price = price; Currency = currency;
    }
    private static string? OptionalLabel(string? value, string parameter)
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100) throw new ArgumentException("Label must contain 1-100 characters.", parameter);
        return value.Trim();
    }
    public string? Category { get; private set; }
    public string? Brand { get; private set; }
    public IReadOnlyList<ProductImage> Images { get; private set; } = Array.Empty<ProductImage>();
    public IReadOnlyList<ProductSpecification> Specifications { get; private set; } = Array.Empty<ProductSpecification>();
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "";
}

public sealed record ProductImage
{
    public ProductImage(string url, string alt, int position)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || url.Length > 2048)
            throw new ArgumentException("Image URL must be an absolute HTTPS URL without credentials.", nameof(url));
        if (string.IsNullOrWhiteSpace(alt) || alt.Trim().Length > 200) throw new ArgumentException("Image alt must contain 1-200 characters.", nameof(alt));
        if (position < 0) throw new ArgumentException("Image position must be non-negative.", nameof(position));
        Url = url; Alt = alt.Trim(); Position = position;
    }
    public string Url { get; }
    public string Alt { get; }
    public int Position { get; }
}
public sealed record ProductSpecification
{
    public ProductSpecification(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Specification name must contain 1-100 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 1000) throw new ArgumentException("Specification value must contain 1-1000 characters.", nameof(value));
        Name = name.Trim(); Value = value.Trim();
    }
    public string Name { get; }
    public string Value { get; }
}
