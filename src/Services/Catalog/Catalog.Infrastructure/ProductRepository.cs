using Catalog.Application;
using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
namespace Catalog.Infrastructure;
public sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public Task<Product?> FindAsync(Guid id, CancellationToken cancellationToken = default) => db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    public async Task<ProductPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default, ProductFilter? filter = null)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) throw new ArgumentException("Invalid pagination.");
        var query = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(filter?.Search)) query = query.Where(p => p.Name.ToLower().Contains(filter.Search.ToLower()) || p.Description.ToLower().Contains(filter.Search.ToLower()));
        if (filter?.Currency is not null) query = query.Where(p => p.Currency == filter.Currency);
        if (filter?.MinPrice is not null) query = query.Where(p => p.Price >= filter.MinPrice);
        if (filter?.MaxPrice is not null) query = query.Where(p => p.Price <= filter.MaxPrice);
        var count = await query.LongCountAsync(cancellationToken);
        var ordered = filter?.Sort switch {
            "name" => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            "name-desc" => query.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            "price" => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price-desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            _ => query.OrderBy(p => p.Id)
        };
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(items, page, pageSize, count);
    }
}
