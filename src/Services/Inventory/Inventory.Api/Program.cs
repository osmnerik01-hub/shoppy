using Observability;
using Inventory.Application;
using Inventory.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.AddObservability("Inventory");
builder.AddDatabaseReadiness<InventoryDbContext>();
builder.AddBrokerReadiness();
builder.Services.AddDbContext<InventoryDbContext>(o => o.UseSqlite(
    builder.Configuration.GetConnectionString("Inventory") ?? "Data Source=inventory.db;Default Timeout=30"));
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<Messaging.IMessageHandler, OrderConsumer>();
if (builder.Configuration["RabbitMQ:Uri"] is string uri)
 builder.Services.AddHostedService(s => new Messaging.BrokerWorker<InventoryDbContext>(s.GetRequiredService<IServiceScopeFactory>(), uri, "inventory.orders.v1", s.GetRequiredService<ILogger<Messaging.BrokerWorker<InventoryDbContext>>>()));
var app = builder.Build();
app.UseObservability();
app.UseExceptionHandler();
app.UseStatusCodePages();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS Outbox (Id TEXT NOT NULL PRIMARY KEY, Payload TEXT NOT NULL, Sent INTEGER NOT NULL)");
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS Inbox (Id TEXT NOT NULL PRIMARY KEY)");
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS OrderDecisions (Id TEXT NOT NULL PRIMARY KEY, Accepted INTEGER NOT NULL)");
    if (builder.Configuration.GetValue<bool>("Inventory:SeedDemo")) {
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var id in new[] { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") })
            if (!await db.StockItems.AnyAsync(x => x.Id == id))
                db.StockItems.Add(new Inventory.Domain.StockItem(id, 100));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
IResult Respond(InventoryResult r) => r.Error is null ? Results.Json(r.Value, statusCode: r.StatusCode)
    : Results.Problem(statusCode: r.StatusCode, title: r.Error);
app.MapGet("/stock/{productId:guid}", async (Guid productId, IInventoryService s, CancellationToken ct) => Respond(await s.GetStock(productId, ct)));
app.MapPost("/stock/{productId:guid}", async (Guid productId, StockRequest request, IInventoryService s, CancellationToken ct) => Respond(await s.AddStock(productId, request.Quantity, ct)));
app.MapPut("/reservations/{id:guid}", async (Guid id, ReserveRequest request, IInventoryService s, CancellationToken ct) => Respond(await s.Reserve(id, request.ProductId, request.Quantity, ct)));
app.MapPost("/reservations/{id:guid}/commit", async (Guid id, IInventoryService s, CancellationToken ct) => Respond(await s.Complete(id, true, ct)));
app.MapPost("/reservations/{id:guid}/release", async (Guid id, IInventoryService s, CancellationToken ct) => Respond(await s.Complete(id, false, ct)));
app.Run();
public record StockRequest(int Quantity);
public record ReserveRequest(Guid ProductId, int Quantity);
public partial class Program { }

