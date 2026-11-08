using Catalog.Domain;
namespace Catalog.Application;
public sealed record ProductPage(IReadOnlyList<Product> Items, int Page, int PageSize, long TotalCount);
public sealed record ProductFilter(string? Search = null, string? Currency = null, decimal? MinPrice = null, decimal? MaxPrice = null, string? Sort = null);
public interface IProductRepository
{
    Task<Product?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default, ProductFilter? filter = null);
}
