using System.Net;
using System.Net.Http.Json;
using Catalog.Domain;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
                await db.GetService<IMigrator>().MigrateAsync("20261103192956_InitialCatalog");
                // Insert user data against the previous schema before upgrading.
                await db.Database.ExecuteSqlRawAsync("INSERT INTO \"Products\" (\"Id\", \"Name\", \"Description\", \"Price\", \"Currency\") VALUES ('33333333-3333-3333-3333-333333333333', 'Legacy', 'User data', 99.99, 'EUR')");
                await db.Database.MigrateAsync();
                Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
                var legacy = await db.Products.SingleAsync(x => x.Id == Guid.Parse("33333333-3333-3333-3333-333333333333"));
                Assert.Equal(99.99m, legacy.Price); Assert.Equal("User data", legacy.Description); Assert.Equal("EUR", legacy.Currency);
                Assert.Null(legacy.Category); Assert.Null(legacy.Brand); Assert.Empty(legacy.Images); Assert.Empty(legacy.Specifications);
                Assert.Equal(750m, (await db.Products.SingleAsync(x => x.Id == Guid.Parse("11111111-1111-1111-1111-111111111111"))).Price);
                await db.Database.MigrateAsync();
                Assert.Equal(3, await db.Products.CountAsync());
                var p = new Product("Test", "Persisted description", 12.34m, "USD", "Electronics", "Fixture", [new("https://example.com/back.png", "Back", 2), new("https://example.com/front.png", "Front", 0)], [new("Connection", "USB")]);
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
                Assert.Equal(4, await invalidDb.Products.CountAsync());
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
            Assert.Equal(4, first!.TotalCount); Assert.Equal(2, first.Items.Length); Assert.Equal(2, second!.Items.Length);
            Assert.Empty(first.Items.Select(p => p.Id).Intersect(second.Items.Select(p => p.Id)));
            Assert.Equal(4, first.Items.Concat(second.Items).Select(p => p.Id).Distinct().Count());
            Assert.Empty((await client.GetFromJsonAsync<Page>("/products?page=10&pageSize=2"))!.Items);
            Assert.Equal(20, (await client.GetFromJsonAsync<Page>("/products"))!.PageSize);
            foreach (var sort in new[] { "name", "name-desc", "price", "price-desc" }) {
                var filtered = await client.GetFromJsonAsync<Page>("/products?category=%20electronics%20&brand=fixture&search=persisted&currency=USD&minPrice=12&maxPrice=13&sort=" + sort + "&pageSize=1");
                Assert.Equal(1, filtered!.TotalCount); Assert.Equal(id, Assert.Single(filtered.Items).Id);
                Assert.Empty((await client.GetFromJsonAsync<Page>("/products?category=Electronics&brand=Fixture&search=Persisted&currency=USD&sort=" + sort + "&pageSize=1&page=2"))!.Items);
            }
            foreach (var mismatch in new[] { "category=Other", "brand=Other", "category=Electronics&brand=Other", "category=Electronics&brand=Fixture&currency=EUR", "category=Electronics&brand=Fixture&search=absent", "category=Electronics&brand=Fixture&currency=USD&minPrice=13" })
                Assert.Equal(0, (await client.GetFromJsonAsync<Page>("/products?" + mismatch))!.TotalCount);
            foreach (var invalid in new[] { "category=", "brand=%20", "category=a&category=b", "brand=a&brand=b", "category=" + new string('x',101), "brand=" + new string('x',101), "currency=XYZ", "sort=invalid", "sort=price", "minPrice=1", "currency=USD&minPrice=-1", "currency=USD&minPrice=abc", "currency=USD&minPrice=20&maxPrice=1", "currency=USD&maxPrice=NaN", "sort=name&sort=price", "search=" + new string('x',201) })
                await AssertProblem(client, "/products?" + invalid, HttpStatusCode.BadRequest);
            await using (var db = new CatalogDbContext(options)) {
                db.Products.AddRange(new Product("Tie", "tie", 15m, "USD", "Accessories", "Fixture"), new Product("Tie", "tie", 15m, "USD", "Accessories", "Fixture")); await db.SaveChangesAsync();
            }
            foreach (var sort in new[] { "name", "name-desc", "price", "price-desc" }) {
                var a = await client.GetFromJsonAsync<Page>("/products?category=accessories&brand=fixture&search=Tie&currency=USD&minPrice=14&maxPrice=16&sort="+sort+"&pageSize=1");
                var b = await client.GetFromJsonAsync<Page>("/products?category=accessories&brand=fixture&search=Tie&currency=USD&minPrice=14&maxPrice=16&sort="+sort+"&pageSize=1&page=2");
                Assert.Equal(2,a!.TotalCount); Assert.Equal(2,b!.TotalCount);
                Assert.True(a.Items[0].Id.CompareTo(b.Items[0].Id)<0);
            }
            var detail = await client.GetFromJsonAsync<Item>($"/products/{id}"); Assert.Equal("Test", detail!.Name);
            Assert.Equal("Electronics", detail.Category); Assert.Equal("Fixture", detail.Brand);
            Assert.Equal(new[] {0, 2}, detail.Images.Select(x => x.Position)); Assert.Equal("USB", Assert.Single(detail.Specifications).Value);
            var legacyDetail = await client.GetFromJsonAsync<Item>("/products/11111111-1111-1111-1111-111111111111");
            Assert.Empty(legacyDetail!.Images); Assert.Empty(legacyDetail.Specifications); Assert.Null(legacyDetail.Brand);
            var listing = await client.GetFromJsonAsync<Page>("/products?category=Electronics&brand=Fixture");
            Assert.Equal(detail.Images, Assert.Single(listing!.Items).Images);
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
    public record Item(Guid Id, string Name, string? Category, string? Brand, ProductImage[] Images, ProductSpecification[] Specifications);
    public record Page(Item[] Items, int PageSize, long TotalCount);
}
