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
}
