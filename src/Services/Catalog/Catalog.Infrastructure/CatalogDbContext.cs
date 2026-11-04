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
        p.HasKey(x => x.Id);
        p.Property(x => x.Name).HasMaxLength(200).IsRequired();
        p.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        p.Property(x => x.Price).HasPrecision(12, 2);
        p.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        p.HasData(new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Klavye", Description = "Kablolu klavye", Price = 750m, Currency = "TRY" },
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Fare", Description = "Optik fare", Price = 350m, Currency = "TRY" });
    }
}
