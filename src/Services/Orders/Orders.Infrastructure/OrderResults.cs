using Messaging;
using Microsoft.EntityFrameworkCore;
namespace Orders.Infrastructure;
public sealed class OrderResults(OrdersDbContext db) : IMessageHandler {
 public async Task Handle(StockEvent m, CancellationToken ct) {
  if (m.Version != 1 || m.Type != "stock.completed.v1") throw new InvalidOperationException("Unsupported event");
  await using var tx = await db.Database.BeginTransactionAsync(ct);
  if (await db.Inbox.AnyAsync(x => x.Id == m.MessageId, ct)) return;
  var order = await db.Orders.SingleAsync(x => x.Id == m.OrderId, ct);
  if (order.ProductId != m.ProductId || order.Quantity != m.Quantity) throw new InvalidOperationException("Event does not match order");
  if (order.Status == "Pending") { order.Status = m.Accepted ? "Confirmed" : "Rejected"; order.RejectionReason = m.Accepted ? null : m.Reason; }
  db.Inbox.Add(new() { Id = m.MessageId });
  await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
 }
}
