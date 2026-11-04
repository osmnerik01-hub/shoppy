using Observability;
using Messaging;
using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using Orders.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.AddObservability("Orders");
builder.AddDatabaseReadiness<OrdersDbContext>();
builder.AddBrokerReadiness();
builder.Services.AddDbContext<OrdersDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Orders") ?? "Data Source=orders.db;Default Timeout=30"));
builder.Services.AddHttpClient("catalog", c => { c.BaseAddress = new Uri(builder.Configuration["Catalog:Url"] ?? "http://localhost:5101"); c.Timeout = TimeSpan.FromSeconds(3); }).AddHttpMessageHandler<CorrelationHandler>();
builder.Services.AddScoped<IMessageHandler, OrderResults>();
if (builder.Configuration["RabbitMQ:Uri"] is string uri)
 builder.Services.AddHostedService(s => new BrokerWorker<OrdersDbContext>(s.GetRequiredService<IServiceScopeFactory>(), uri, "orders.results.v1", s.GetRequiredService<ILogger<BrokerWorker<OrdersDbContext>>>()));
var app = builder.Build();
app.UseObservability();
app.UseExceptionHandler();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.EnsureCreatedAsync();
app.MapGet("/orders/{id:guid}", async (Guid id, OrdersDbContext db, CancellationToken ct) => await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is Order order ? Results.Ok(order) : Results.NotFound());
app.MapPost("/orders", async (CreateOrder request, HttpContext http, OrdersDbContext db, IHttpClientFactory clients, CancellationToken ct) => {
 var key = http.Request.Headers["Idempotency-Key"].ToString();
 if (string.IsNullOrWhiteSpace(key) || key.Length > 200 || request.ProductId == Guid.Empty || request.Quantity <= 0) return Results.BadRequest();
 async Task<IResult?> Replay() {
  var old = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
  return old is null ? null : old.ProductId == request.ProductId && old.Quantity == request.Quantity ? Results.Accepted($"/orders/{old.Id}", old) : Results.Conflict();
 }
 var replay = await Replay(); if (replay is not null) return replay;
 CatalogPrice? product;
 try {
  using var response = await clients.CreateClient("catalog").GetAsync($"/products/{request.ProductId}", ct);
  if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return Results.NotFound();
  response.EnsureSuccessStatusCode(); product = await response.Content.ReadFromJsonAsync<CatalogPrice>(ct);
 } catch (System.Text.Json.JsonException) { return Results.Problem(statusCode: 503, title: "Invalid catalog response"); }
 catch (HttpRequestException) { return Results.Problem(statusCode: 503, title: "Catalog unavailable"); }
 catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return Results.Problem(statusCode: 503, title: "Catalog timeout"); }
 if (product is null || product.Id != request.ProductId || product.Price <= 0 || product.Currency is not ("TRY" or "USD" or "EUR")) return Results.Problem(statusCode: 503, title: "Invalid catalog response");
 // Acquire SQLite write lock before replay check: concurrent keys serialize across instances.
 await using var tx = await db.Database.BeginTransactionAsync(ct);
 replay = await Replay(); if (replay is not null) return replay;
 var order = new Order { IdempotencyKey = key, ProductId = request.ProductId, Quantity = request.Quantity, UnitPrice = product.Price, Currency = product.Currency };
 db.Orders.Add(order);
 db.Enqueue(new(Guid.NewGuid(), "order.placed.v1", 1, order.Id, order.ProductId, order.Quantity));
 await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
 return Results.Accepted($"/orders/{order.Id}", order);
});
app.Run();
public record CreateOrder(Guid ProductId, int Quantity);
public record CatalogPrice(Guid Id, decimal Price, string Currency);
public partial class Program { }

