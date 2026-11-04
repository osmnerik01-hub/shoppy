using Orders.Domain;

namespace Orders.Application;

public interface IOrderRepository
{
    Task<Order?> FindAsync(Guid id, CancellationToken cancellationToken = default);
}

