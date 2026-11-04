using System.Net;
using System.Net.Http.Json;
using Catalog.Domain;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

public class CatalogTests
{
    [Fact]
    public async Task PostgreSqlMigrationPersistencePaginationAndErrors()
    {
        var connection = Environment.GetEnvironmentVariable("CATALOG_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Set CATALOG_TEST_POSTGRES to a real PostgreSQL admin connection; test creates and drops its own database.");
        var name = "catalog_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", admin)) await create.ExecuteNonQueryAsync();
        var testConnection = new NpgsqlConnectionStringBuilder(connection) { Database = name, Pooling = false }.ConnectionString;
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(testConnection).Options;
        try
        {
            Guid id;
            await using (var db = new CatalogDbContext(options))
            {
                await db.Database.MigrateAsync();
                Assert.Single(await db.Database.GetAppliedMigrationsAsync());
                await db.Database.MigrateAsync();
                Assert.Equal(2, await db.Products.CountAsync());
                var p = new Product("Test", "Persisted description", 12.34m, "USD");
                id = p.Id; db.Products.Add(p); await db.SaveChangesAsync();
                await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO \"Products\" (\"Id\", \"Name\", \"Description\", \"Price\", \"Currency\") VALUES (gen_random_uuid(), '', 'd', 1, 'TRY')"));
            }
            await using (var invalidDb = new CatalogDbContext(options))
            {
                foreach (var values in new[] { "'n', '', 1, 'TRY'", "'n', 'd', 0, 'TRY'", "'n', 'd', -1, 'TRY'", "'n', 'd', 1, 'XYZ'", "repeat('n', 201), 'd', 1, 'TRY'", "'n', repeat('d', 4001), 1, 'TRY'" })
                {
                    var sql = "INSERT INTO \"Products\" (\"Id\", \"Name\", \"Description\", \"Price\", \"Currency\") VALUES (gen_random_uuid(), " + values + ")";
                    var error = await Assert.ThrowsAsync<PostgresException>(() => invalidDb.Database.ExecuteSqlRawAsync(sql));
                    Assert.Contains(error.SqlState, new[] { "23514", "22001" });
                }
                Assert.Equal(3, await invalidDb.Products.CountAsync());
            }
            await using (var db = new CatalogDbContext(options))
            {
                var p = await db.Products.SingleAsync(p => p.Id == id);
                Assert.Equal(12.34m, p.Price); Assert.Equal("Persisted description", p.Description); Assert.Equal("USD", p.Currency);
            }
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Catalog", testConnection));
            using var client = factory.CreateClient();
            var first = await client.GetFromJsonAsync<Page>("/products?page=1&pageSize=2");
            var second = await client.GetFromJsonAsync<Page>("/products?page=2&pageSize=2");
            Assert.Equal(3, first!.TotalCount); Assert.Equal(2, first.Items.Length); Assert.Single(second!.Items);
            Assert.Empty(first.Items.Select(p => p.Id).Intersect(second.Items.Select(p => p.Id)));
            Assert.Equal(3, first.Items.Concat(second.Items).Select(p => p.Id).Distinct().Count());
            Assert.Empty((await client.GetFromJsonAsync<Page>("/products?page=10&pageSize=2"))!.Items);
            Assert.Equal(20, (await client.GetFromJsonAsync<Page>("/products"))!.PageSize);
            var detail = await client.GetFromJsonAsync<Item>($"/products/{id}"); Assert.Equal("Test", detail!.Name);
            foreach (var url in new[] { "/products?page=0", "/products?pageSize=101", "/products?pageSize=0", "/products?page=abc", "/products?page=2147483647&pageSize=100", "/products/no-guid" })
                await AssertProblem(client, url, HttpStatusCode.BadRequest);
            await AssertProblem(client, $"/products/{Guid.NewGuid()}", HttpStatusCode.NotFound);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private static async Task AssertProblem(HttpClient client, string url, HttpStatusCode status)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
    }
    public record Item(Guid Id, string Name);
    public record Page(Item[] Items, int PageSize, long TotalCount);
}
