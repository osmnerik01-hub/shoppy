using Inventory.Domain;
namespace Inventory.Application;

public record InventoryResult(int StatusCode, object? Value = null, string? Error = null);
public interface IInventoryService
{
    Task<InventoryResult> AddStock(Guid productId, int quantity, CancellationToken ct);
    Task<InventoryResult> GetStock(Guid productId, CancellationToken ct);
    Task<InventoryResult> Reserve(Guid id, Guid productId, int quantity, CancellationToken ct);
    Task<InventoryResult> Complete(Guid id, bool commit, CancellationToken ct);
}
