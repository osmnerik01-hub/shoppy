using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
public class GatewayTests {
 [Fact]
 public async Task Routes_correlation_readiness_and_outage() {
  var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
  await using var downstream = builder.Build();
  var ready = true; var posts = 0;
  downstream.MapGet("/health/ready", () => Microsoft.AspNetCore.Http.Results.StatusCode(ready ? 200 : 503));
  downstream.MapGet("/products/{id}", (string id, Microsoft.AspNetCore.Http.HttpContext c) => c.Request.Headers["X-Correlation-ID"].ToString() + ":" + id);
  downstream.MapGet("/products/slow", async () => { await Task.Delay(TimeSpan.FromSeconds(7)); return "late"; });
  downstream.MapPost("/orders", () => { posts++; return Microsoft.AspNetCore.Http.Results.StatusCode(ready ? 202 : 503); });
  await downstream.StartAsync();
  var url = downstream.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
  await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
   ["ReverseProxy:Clusters:catalog:Destinations:primary:Address"] = url,
   ["ReverseProxy:Clusters:orders:Destinations:primary:Address"] = url
  })));
  using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Correlation-ID", "test-123");
  var response = await client.GetAsync("/api/products/abc");
  Assert.Equal("test-123:abc", await response.Content.ReadAsStringAsync());
  Assert.Equal("test-123", response.Headers.GetValues("X-Correlation-ID").Single());
  Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/orders", null)).StatusCode); Assert.Equal(1, posts);
  Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/stock/abc")).StatusCode);
  Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
  var elapsed = System.Diagnostics.Stopwatch.StartNew();
  Assert.Equal(HttpStatusCode.GatewayTimeout, (await client.GetAsync("/api/products/slow")).StatusCode);
  Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10));
  ready = false;
  Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/orders", null)).StatusCode);
  Assert.Equal(2, posts);
  Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
  Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
  await downstream.StopAsync();
  Assert.Equal(HttpStatusCode.BadGateway, (await client.PostAsync("/api/orders", null)).StatusCode);
  Assert.Equal(2, posts);
  Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
 }
}
