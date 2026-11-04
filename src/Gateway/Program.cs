using Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
var builder = WebApplication.CreateBuilder(args);
builder.AddObservability("Gateway");
builder.Services.AddRequestTimeouts(o => o.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(10) });
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
foreach (var service in new[] { "catalog", "orders" }) {

 builder.Services.AddHealthChecks().AddAsyncCheck(service, async ct => {
  var address = builder.Configuration[$"ReverseProxy:Clusters:{service}:Destinations:primary:Address"]!;
  using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
  using var response = await client.GetAsync(new Uri(new Uri(address), "health/ready"), ct);
  return response.IsSuccessStatusCode ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Dependency unavailable");
 }, tags: ["ready"], timeout: TimeSpan.FromSeconds(3));
}
var app = builder.Build();
app.UseObservability();
app.UseRequestTimeouts();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapReverseProxy();
app.Run();
public partial class Program { }
