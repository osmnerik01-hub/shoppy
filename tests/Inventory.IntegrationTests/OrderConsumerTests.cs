using Inventory.Domain;
using Inventory.Infrastructure;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
public sealed class OrderConsumerTests {
 sealed class FailSave : SaveChangesInterceptor {
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default) => throw new IOException("simulated crash before outbox commit");
 }
 [Fact]
 public async Task Failure_rolls_back_stock_and_retry_is_deduplicated() {
  var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
  var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
  var product = Guid.NewGuid(); var message = new StockEvent(Guid.NewGuid(), "order.placed.v1", 1, Guid.NewGuid(), product, 2);
  try {
   await using (var db = new InventoryDbContext(options)) { await db.Database.EnsureCreatedAsync(); db.StockItems.Add(new StockItem(product, 3)); await db.SaveChangesAsync(); }
   var failing = new DbContextOptionsBuilder<InventoryDbContext>(options).AddInterceptors(new FailSave()).Options;
   await using (var db = new InventoryDbContext(failing)) await Assert.ThrowsAsync<IOException>(() => new OrderConsumer(db).Handle(message, default));
   await using (var db = new InventoryDbContext(options)) { Assert.Equal(3, (await db.StockItems.SingleAsync()).Quantity); Assert.Empty(await db.Reservations.ToListAsync()); Assert.Empty(await db.Outbox.ToListAsync()); Assert.Empty(await db.Inbox.ToListAsync()); }
   foreach (var m in new[] { message, message, message with { MessageId = Guid.NewGuid() } }) { await using var db = new InventoryDbContext(options); await new OrderConsumer(db).Handle(m, default); }
   await using (var db = new InventoryDbContext(options)) { Assert.Equal(1, (await db.StockItems.SingleAsync()).Quantity); Assert.Single(await db.Reservations.ToListAsync()); Assert.Single(await db.Outbox.ToListAsync()); }
  } finally { File.Delete(path); }
 }
 [Fact]
 public async Task Rejection_remains_final_after_restock_and_unknown_versions_do_not_write() {
  await using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite("Data Source=:memory:").Options);
  await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
  var product = Guid.NewGuid(); var m = new StockEvent(Guid.NewGuid(), "order.placed.v1", 1, Guid.NewGuid(), product, 1);
  var handler = new OrderConsumer(db); await handler.Handle(m, default);
  db.StockItems.Add(new StockItem(product, 5)); await db.SaveChangesAsync();
  await handler.Handle(m with { MessageId = Guid.NewGuid() }, default);
  Assert.Equal(5, (await db.StockItems.SingleAsync()).Quantity); Assert.False((await db.OrderDecisions.SingleAsync()).Accepted); Assert.Single(await db.Outbox.ToListAsync());
  await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(m with { Version = 2 }, default));
 }
}
