using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
namespace Observability;
public static class ServiceDefaults {
 public static void AddObservability(this WebApplicationBuilder builder, string name) {
  builder.Logging.ClearProviders();
  builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "O"; });
  builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService(name))
   .WithTracing(t => {
       t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
       if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
           t.AddOtlpExporter();
   });
  builder.Services.AddHealthChecks();
  builder.Services.AddHttpContextAccessor();
  builder.Services.AddTransient<CorrelationHandler>();
 }
 public static void AddDatabaseReadiness<T>(this WebApplicationBuilder builder) where T : DbContext {
  builder.Services.AddHealthChecks().AddCheck<DatabaseCheck<T>>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(2));
 }
 public static void AddBrokerReadiness(this WebApplicationBuilder builder) {
  var uri = builder.Configuration["RabbitMQ:Uri"];
  builder.Services.AddHealthChecks().AddAsyncCheck("broker", async ct => {
   if (string.IsNullOrWhiteSpace(uri)) return HealthCheckResult.Unhealthy("Broker is not configured");
   await using var connection = await new ConnectionFactory { Uri = new Uri(uri), RequestedConnectionTimeout = TimeSpan.FromSeconds(2) }.CreateConnectionAsync(ct);
   return connection.IsOpen ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
  }, tags: ["ready"], timeout: TimeSpan.FromSeconds(3));
 }
 public static void UseObservability(this WebApplication app) {
  app.Use(async (context, next) => {
   var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
   var id = supplied.Length is > 0 and <= 64 && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? supplied : Guid.NewGuid().ToString("N");
   context.Request.Headers["X-Correlation-ID"] = id;
   context.Response.Headers["X-Correlation-ID"] = id;
   Activity.Current?.SetTag("correlation.id", id);
   using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id, ["TraceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier });
   var start = Stopwatch.GetTimestamp();
   try { await next(context); }
   finally { app.Logger.LogInformation("HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs} ms", context.Request.Method, context.Request.Path, context.Response.StatusCode, Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
  });
  app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
  app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
  app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
 }
}
public sealed class DatabaseCheck<T>(IServiceScopeFactory scopes) : IHealthCheck where T : DbContext {
 public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) {
  using var scope = scopes.CreateScope();
  return await scope.ServiceProvider.GetRequiredService<T>().Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database unavailable");
 }
}

public sealed class CorrelationHandler(Microsoft.AspNetCore.Http.IHttpContextAccessor accessor) : DelegatingHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
  var id = accessor.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();
  if (!string.IsNullOrEmpty(id)) request.Headers.TryAddWithoutValidation("X-Correlation-ID", id);
  return base.SendAsync(request, ct);
 }
}
