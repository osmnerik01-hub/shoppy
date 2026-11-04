using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Catalog.Infrastructure;
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(
        Environment.GetEnvironmentVariable("ConnectionStrings__Catalog") ?? "Host=localhost;Database=catalog;Username=postgres").Options);
}
