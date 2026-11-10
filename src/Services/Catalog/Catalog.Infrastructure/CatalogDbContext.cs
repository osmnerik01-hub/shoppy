using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
namespace Catalog.Infrastructure;
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var p = modelBuilder.Entity<Product>();
        p.ToTable("Products", t => {
            t.HasCheckConstraint("CK_Product_Name", "length(btrim(\"Name\")) BETWEEN 1 AND 200");
            t.HasCheckConstraint("CK_Product_Description", "length(btrim(\"Description\")) BETWEEN 1 AND 4000");
            t.HasCheckConstraint("CK_Product_Price", "\"Price\" > 0");
            t.HasCheckConstraint("CK_Product_Currency", "\"Currency\" IN ('TRY', 'USD', 'EUR')");
        });
        p.Property(x => x.Category).HasMaxLength(100);
        p.Property(x => x.Brand).HasMaxLength(100);
        ConfigureJson(p.Property(x => x.Images));
        ConfigureJson(p.Property(x => x.Specifications));
        p.HasIndex(x => new { x.Category, x.Brand });
        p.HasKey(x => x.Id);
        p.Property(x => x.Name).HasMaxLength(200).IsRequired();
        p.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        p.Property(x => x.Price).HasPrecision(12, 2);
        p.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        p.HasData(new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Klavye", Description = "Kablolu klavye", Price = 750m, Currency = "TRY" },
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Fare", Description = "Optik fare", Price = 350m, Currency = "TRY" });
    }
    private static void ConfigureJson<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<IReadOnlyList<T>> property)
    {
        property.HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => Array.AsReadOnly(System.Text.Json.JsonSerializer.Deserialize<T[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!));
        property.Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyList<T>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            v => Array.AsReadOnly(v.ToArray())));
    }
}
