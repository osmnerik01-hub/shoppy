using Catalog.Domain;
using Xunit;
public class ProductTests
{
    [Theory]
    [InlineData("", "description", 1, "TRY")]
    [InlineData("name", " ", 1, "TRY")]
    [InlineData("name", "description", 0, "TRY")]
    [InlineData("name", "description", -1, "TRY")]
    [InlineData("name", "description", 1.001, "TRY")]
    [InlineData("name", "description", 10000000000, "TRY")]
    [InlineData("name", "description", 1, "try")]
    [InlineData("name", "description", 1, "XYZ")]
    public void RejectsInvalidValues(string name, string description, decimal price, string currency) =>
        Assert.Throws<ArgumentException>(() => new Product(name, description, price, currency));
    [Fact]
    public void EnforcesTextLimits()
    {
        Assert.Throws<ArgumentException>(() => new Product(new string('a', 201), "d", 1, "TRY"));
        Assert.Throws<ArgumentException>(() => new Product("n", new string('a', 4001), 1, "TRY"));
        var p = new Product(" name ", new string('a', 4000), 9999999999.99m, "EUR");
        Assert.Equal("name", p.Name);
    }

    [Fact]
    public void EnrichmentIsValidatedOrderedAndOptional()
    {
        var legacy = new Product("n", "d", 1, "TRY");
        Assert.Null(legacy.Category); Assert.Null(legacy.Brand); Assert.Empty(legacy.Images); Assert.Empty(legacy.Specifications);
        var images = new[] { new ProductImage("https://example.com/b.png", "Back", 2), new ProductImage("https://example.com/a.png", "Front", 0) };
        var p = new Product("n", "d", 1, "TRY", " Electronics ", " Brand ", images, [new(" Connection ", " USB ")]);
        Assert.Equal("Electronics", p.Category); Assert.Equal("Brand", p.Brand);
        Assert.Equal(new[] {0,2}, p.Images.Select(x => x.Position)); Assert.Equal("USB", p.Specifications[0].Value);
        Assert.Throws<ArgumentException>(() => new Product("n", "d", 1, "TRY", " "));
        Assert.Throws<ArgumentException>(() => new Product("n", "d", 1, "TRY", brand: new string('x',101)));
        Assert.Throws<ArgumentException>(() => new Product("n", "d", 1, "TRY", images: [images[0],images[0]]));
        Assert.Throws<ArgumentException>(() => new Product("n", "d", 1, "TRY", specifications: [new("Port", "USB"),new("port", "USB")]));
        foreach (var url in new[] {"javascript:alert(1)","http://example.com/a", "/a", "https://user:secret@example.com/a"})
            Assert.Throws<ArgumentException>(() => new ProductImage(url, "alt", 0));
        Assert.Throws<ArgumentException>(() => new ProductImage("https://example.com/a", " ", 0));
        Assert.Throws<ArgumentException>(() => new ProductImage("https://example.com/a", "alt", -1));
        Assert.Throws<ArgumentException>(() => new ProductSpecification(" ", "v"));
        Assert.Throws<ArgumentException>(() => new ProductSpecification("n", new string('x',1001)));
    }
}
