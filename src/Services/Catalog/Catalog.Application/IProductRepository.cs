using Catalog.Domain;
namespace Catalog.Application;
public sealed record ProductPage(IReadOnlyList<Product> Items, int Page, int PageSize, long TotalCount);
public interface IProductRepository
{
    Task<Product?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
