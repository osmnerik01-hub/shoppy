using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inventory.Domain;
using Inventory.Infrastructure;
using Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Domain;
using Orders.Infrastructure;
using Xunit;
public sealed class OrderFlowTests {
 sealed class Catalog : HttpMessageHandler {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { id = Guid.Parse(r.RequestUri!.Segments.Last()), name = "Consumer fixture", description = "Catalog compatibility fixture", price = 42.50m, currency = "TRY", category = "Test category", brand = "Test brand", images = new[] { new { url = "https://example.com/fixture.png", alt = "Fixture", position = 0 } }, specifications = new[] { new { name = "Connection", value = "USB" } } }) });
 }
 [Theory]
 [InlineData(5, "Confirmed", 3)]
 [InlineData(1, "Rejected", 1)]
 public async Task Snapshot_idempotency_outage_redelivery_and_stock(int stock, string status, int remaining) {
  var folder = Path.Combine(Path.GetTempPath(), "order-flow-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
  try {
   var oc = $"Data Source={folder}/orders.db;Pooling=False";
   var ic = $"Data Source={folder}/inventory.db;Pooling=False";
   await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
    b.UseSetting("ConnectionStrings:Orders", oc);
    b.ConfigureServices(s => s.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(() => new Catalog()));
   });
   using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("Idempotency-Key", "same");
   var product = Guid.NewGuid();
   var response = await client.PostAsJsonAsync("/orders", new { productId = product, quantity = 2, price = 0.01, currency = "USD" });
   Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
   var order = (await response.Content.ReadFromJsonAsync<Order>())!;
   Assert.Equal(42.50m, order.UnitPrice); Assert.Equal("TRY", order.Currency); Assert.Equal("Pending", order.Status);
   var again = await client.PostAsJsonAsync("/orders", new { productId = product, quantity = 2 });
   Assert.Equal(order.Id, (await again.Content.ReadFromJsonAsync<Order>())!.Id);
   Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/orders", new { productId = product, quantity = 3 })).StatusCode);
   var oo = new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(oc).Options;
   var io = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(ic).Options;
   await using (var db = new OrdersDbContext(oo)) {
    Assert.Single(await db.Outbox.ToListAsync());
    await Assert.ThrowsAsync<IOException>(() => OutboxDispatch.Send(db, _ => throw new IOException("HTTP consumer unavailable")));
   }
   StockEvent placed;
   await using (var db = new OrdersDbContext(oo)) { Assert.False((await db.Outbox.SingleAsync()).Sent); placed = JsonSerializer.Deserialize<StockEvent>((await db.Outbox.SingleAsync()).Payload)!; }
   await using (var db = new InventoryDbContext(io)) { await db.Database.EnsureCreatedAsync(); db.StockItems.Add(new StockItem(product, stock)); await db.SaveChangesAsync(); }
   for (int i = 0; i < 2; i++) { await using var db = new InventoryDbContext(io); await new OrderConsumer(db).Handle(placed, default); }
   StockEvent result;
   await using (var db = new InventoryDbContext(io)) {
    Assert.Equal(remaining, (await db.StockItems.SingleAsync()).Quantity); Assert.Equal(0, (await db.StockItems.SingleAsync()).Reserved);
    Assert.Equal(status == "Confirmed" ? 1 : 0, await db.Reservations.CountAsync());
    result = JsonSerializer.Deserialize<StockEvent>((await db.Outbox.SingleAsync()).Payload)!;
   }
   Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/internal/messages", result with { Version = 2 })).StatusCode);
   for (int i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/internal/messages", result)).StatusCode);
   await using (var db = new OrdersDbContext(oo)) { Assert.Equal(status, (await db.Orders.SingleAsync()).Status); Assert.Single(await db.Inbox.ToListAsync()); }
   await using (var db = new OrdersDbContext(oo)) { await OutboxDispatch.Send(db, _ => Task.CompletedTask); Assert.True((await db.Outbox.SingleAsync()).Sent); }
  } finally { Directory.Delete(folder, true); }
 }
}
