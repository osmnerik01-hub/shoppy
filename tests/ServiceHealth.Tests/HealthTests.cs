extern alias OrdersApi;
extern alias InventoryApi;
using System.Net;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
public class HealthTests {
 [Fact]
 public async Task Catalog_database_failure_changes_readiness_but_not_liveness() {
  var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".db");
  try {
   await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
    b.UseSetting("ConnectionStrings:Catalog", "Host=unused");
    b.ConfigureServices(s => {
     s.RemoveAll<DbContextOptions<CatalogDbContext>>();
     s.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<CatalogDbContext>>();
     s.AddDbContext<CatalogDbContext>(o => o.UseSqlite("Data Source="+path+";Mode=ReadWrite;Pooling=False"));
    });
   });
   using var client = factory.CreateClient();
   Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
   Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
   using (File.Create(path)) { }
   Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
   File.Delete(path);
   Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
  } finally { File.Delete(path); }
 }
 [Fact]
 public async Task Orders_and_inventory_are_ready_without_broker() {
  var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(folder);
  try {
   await using var orders = new WebApplicationFactory<OrdersApi::Program>().WithWebHostBuilder(b => { b.UseSetting("ConnectionStrings:Orders", "Data Source="+folder+"/o.db;Pooling=False"); });
   await using var inventory = new WebApplicationFactory<InventoryApi::Program>().WithWebHostBuilder(b => { b.UseSetting("ConnectionStrings:Inventory", "Data Source="+folder+"/i.db;Pooling=False"); });
   foreach (var client in new[] { orders.CreateClient(), inventory.CreateClient() }) {
    using (client) {
     Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
     Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
   }
  } finally { Directory.Delete(folder, true); }
 }
}
