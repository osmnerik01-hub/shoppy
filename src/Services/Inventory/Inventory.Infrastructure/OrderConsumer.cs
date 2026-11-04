using Messaging;
using Inventory.Domain;
using Microsoft.EntityFrameworkCore;
namespace Inventory.Infrastructure;
public sealed class OrderConsumer(InventoryDbContext db) : IMessageHandler {
 public async Task Handle(StockEvent m, CancellationToken ct) {
  if (m.Version != 1 || m.Type != "order.placed.v1" || m.OrderId == Guid.Empty || m.ProductId == Guid.Empty || m.Quantity <= 0) throw new InvalidOperationException("Invalid event");
  await using var tx = await db.Database.BeginTransactionAsync(ct);
  if (await db.Inbox.AnyAsync(x => x.Id == m.MessageId, ct)) return;
  if (await db.OrderDecisions.AnyAsync(x => x.Id == m.OrderId, ct)) {
   db.Inbox.Add(new() { Id = m.MessageId }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return;
  }
  var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.Id == m.OrderId, ct);
  bool accepted;
  if (reservation is not null) accepted = reservation.ProductId == m.ProductId && reservation.Quantity == m.Quantity && reservation.Status == "Committed";
  else {
   accepted = await db.StockItems.Where(x => x.Id == m.ProductId && x.Quantity - x.Reserved >= m.Quantity)
    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity - m.Quantity), ct) == 1;
   if (accepted) { reservation = new Reservation(m.OrderId, m.ProductId, m.Quantity); reservation.Complete(true); db.Reservations.Add(reservation); }
  }
  db.OrderDecisions.Add(new() { Id = m.OrderId, Accepted = accepted });
  db.Inbox.Add(new() { Id = m.MessageId });
  db.Enqueue(new(Guid.NewGuid(), "stock.completed.v1", 1, m.OrderId, m.ProductId, m.Quantity, accepted, accepted ? null : "Insufficient stock or conflicting reservation"));
  await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
 }
}
