using Observability;
using Catalog.Application;
using Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.AddObservability("Catalog");
builder.AddDatabaseReadiness<CatalogDbContext>();
builder.Services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Catalog") ?? throw new InvalidOperationException("ConnectionStrings:Catalog is required.")));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
var app = builder.Build();
app.UseObservability();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Configuration.GetValue<bool>("Catalog:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
}
app.MapGet("/products", async (HttpContext context, IProductRepository repository, CancellationToken ct) =>
{
    var query = context.Request.Query;
    int page = 1, size = 20;
    if ((query.ContainsKey("page") && !int.TryParse(query["page"], out page)) ||
        (query.ContainsKey("pageSize") && !int.TryParse(query["pageSize"], out size)) ||
        page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
        return Results.Problem(statusCode: 400, title: "Invalid pagination", detail: "page >= 1, pageSize between 1 and 100; offset must fit Int32.");
    return Results.Ok(await repository.ListAsync(page, size, ct));
});
app.MapGet("/products/{id}", async (string id, IProductRepository repository, CancellationToken ct) =>
{
    if (!Guid.TryParse(id, out var key)) return Results.Problem(statusCode: 400, title: "Invalid product id");
    var product = await repository.FindAsync(key, ct);
    return product is null ? Results.Problem(statusCode: 404, title: "Product not found") : Results.Ok(product);
});
app.Run();
public partial class Program { }
