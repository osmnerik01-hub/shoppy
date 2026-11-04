using System.Net;
using System.Net.Http.Json;
using Inventory.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class InventoryTests : IAsyncLifetime
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "inventory-test-" + Guid.NewGuid() + ".db");
    private WebApplicationFactory<Program> first = null!, second = null!;
    private HttpClient a = null!, b = null!;
    private string Connection => $"Data Source={path};Pooling=False;Default Timeout=30";
    private DbContextOptions<InventoryDbContext> Options => new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(Connection).Options;
    public Task InitializeAsync()
    {
        first = new WebApplicationFactory<Program>().WithWebHostBuilder(h => h.UseSetting("ConnectionStrings:Inventory", Connection));
        a = first.CreateClient();
        second = new WebApplicationFactory<Program>().WithWebHostBuilder(h => h.UseSetting("ConnectionStrings:Inventory", Connection));
        b = second.CreateClient();
        return Task.CompletedTask;
    }
    public async Task DisposeAsync()
    {
        a.Dispose(); b.Dispose(); await first.DisposeAsync(); await second.DisposeAsync(); File.Delete(path);
    }
    private async Task<Guid> Stock(int quantity)
    {
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Created, (await a.PostAsJsonAsync($"/stock/{id}", new { quantity })).StatusCode);
        return id;
    }
    private Task<HttpResponseMessage> Reserve(HttpClient client, Guid id, Guid productId, int quantity = 1) => client.PutAsJsonAsync($"/reservations/{id}", new { productId, quantity });
    private async Task Check(Guid product, int quantity, int reserved, int reservations)
    {
        // A fresh connection proves committed persistence independently of request scopes.
        await using var db = new InventoryDbContext(Options);
        var stock = await db.StockItems.SingleAsync(x => x.Id == product);
        Assert.Equal(quantity, stock.Quantity); Assert.Equal(reserved, stock.Reserved);
        Assert.True(stock.Available >= 0);
        Assert.Equal(reservations, await db.Reservations.CountAsync());
    }
    [Fact]
    public async Task SeparateServiceInstancesCompeteForLastUnit()
    {
        var product = await Stock(1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 16).Select(i => Task.Run(async () => {
            await start.Task;
            return await Reserve(i % 2 == 0 ? a : b, Guid.NewGuid(), product);
        })).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(requests);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(15, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await Check(product, 1, 1, 1);
    }
    [Fact]
    public async Task ConcurrentDuplicateReservationOnlyDeductsOnceAndRejectsChangedPayload()
    {
        var product = await Stock(5); var id = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(() => Reserve(i % 2 == 0 ? a : b, id, product, 2))));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(HttpStatusCode.Conflict, (await Reserve(a, id, product, 3)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Reserve(b, id, Guid.NewGuid(), 2)).StatusCode);
        await Check(product, 5, 2, 1);
    }
    [Fact]
    public async Task InsufficientAndInvalidStockLeaveNoReservation()
    {
        var product = await Stock(1);
        Assert.Equal(HttpStatusCode.Conflict, (await Reserve(a, Guid.NewGuid(), product, 2)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Reserve(a, Guid.NewGuid(), Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reserve(a, Guid.NewGuid(), product, 0)).StatusCode);
        await Check(product, 1, 0, 0);
    }
    [Theory]
    [InlineData("commit", "release", 1)]
    [InlineData("release", "commit", 3)]
    public async Task FinalizationIsIdempotentAndDuplicateCreationDoesNotResurrect(string action, string opposite, int remaining)
    {
        var product = await Stock(3); var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await Reserve(a, id, product, 2)).StatusCode);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => (i % 2 == 0 ? a : b).PostAsync($"/reservations/{id}/{action}", null))));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(HttpStatusCode.Conflict, (await b.PostAsync($"/reservations/{id}/{opposite}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Reserve(b, id, product, 2)).StatusCode);
        await Check(product, remaining, 0, 1);
    }
    [Fact]
    public async Task CompetingCommitAndReleaseApplyExactlyOneTransition()
    {
        var product = await Stock(2); var id = Guid.NewGuid();
        await Reserve(a, id, product, 2);
        var results = await Task.WhenAll(Task.Run(() => a.PostAsync($"/reservations/{id}/commit", null)), Task.Run(() => b.PostAsync($"/reservations/{id}/release", null)));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = new InventoryDbContext(Options);
        var reservation = await db.Reservations.SingleAsync();
        await Check(product, reservation.Status == "Committed" ? 0 : 2, 0, 1);
    }
    [Fact]
    public async Task DatabaseRejectsNegativeAvailability()
    {
        var product = await Stock(1);
        await using var db = new InventoryDbContext(Options);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockItems SET Reserved = 2 WHERE Id = {product}"));
        await Check(product, 1, 0, 0);
    }
}

