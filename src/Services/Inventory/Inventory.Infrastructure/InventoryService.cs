using Inventory.Application;
using Inventory.Domain;
using Microsoft.EntityFrameworkCore;
namespace Inventory.Infrastructure;

public sealed class InventoryService(InventoryDbContext db) : IInventoryService
{
    public async Task<InventoryResult> GetStock(Guid productId, CancellationToken ct)
    {
        var stock = await db.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == productId, ct);
        return stock is null ? new(404, Error: "Product stock not found") : new(200, stock);
    }
    public async Task<InventoryResult> AddStock(Guid productId, int quantity, CancellationToken ct)
    {
        if (productId == Guid.Empty || quantity < 0) return new(400, Error: "Valid product id and nonnegative quantity required");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.StockItems.AnyAsync(x => x.Id == productId, ct)) return new(409, Error: "Stock already exists");
        var stock = new StockItem(productId, quantity);
        db.StockItems.Add(stock);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(201, stock);
    }
    public async Task<InventoryResult> Reserve(Guid id, Guid productId, int quantity, CancellationToken ct)
    {
        if (id == Guid.Empty || productId == Guid.Empty || quantity <= 0) return new(400, Error: "Valid ids and positive quantity required");
        // SQLite serializable transactions acquire the database write lock before any read.
        // This works across service instances; no process-local lock is used.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (existing is not null)
            return existing.ProductId == productId && existing.Quantity == quantity
                ? new(200, existing) : new(409, Error: "Reservation id already used with different contents");
        var changed = await db.StockItems.Where(x => x.Id == productId && x.Quantity - x.Reserved >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Reserved, x => x.Reserved + quantity), ct);
        if (changed == 0) return new(409, Error: "Insufficient stock or unknown product");
        var reservation = new Reservation(id, productId, quantity);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(200, reservation);
    }
    public async Task<InventoryResult> Complete(Guid id, bool commit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (reservation is null) return new(404, Error: "Reservation not found");
        var target = commit ? "Committed" : "Released";
        if (reservation.Status == target) return new(200, reservation);
        if (reservation.Status != "Reserved") return new(409, Error: "Reservation already finalized differently");
        await db.StockItems.Where(x => x.Id == reservation.ProductId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Reserved, x => x.Reserved - reservation.Quantity)
            .SetProperty(x => x.Quantity, x => x.Quantity - (commit ? reservation.Quantity : 0)), ct);
        reservation.Complete(commit);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(200, reservation);
    }
}
