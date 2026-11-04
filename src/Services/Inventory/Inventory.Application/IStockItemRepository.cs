using Inventory.Domain;

namespace Inventory.Application;

public interface IStockItemRepository
{
    Task<StockItem?> FindAsync(Guid id, CancellationToken cancellationToken = default);
}

