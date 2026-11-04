using Catalog.Application;
using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
namespace Catalog.Infrastructure;
public sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public Task<Product?> FindAsync(Guid id, CancellationToken cancellationToken = default) => db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    public async Task<ProductPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) throw new ArgumentException("Invalid pagination.");
        var count = await db.Products.LongCountAsync(cancellationToken);
        var items = await db.Products.AsNoTracking().OrderBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(items, page, pageSize, count);
    }
}
