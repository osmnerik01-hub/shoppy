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
    string? search = query.ContainsKey("search") ? query["search"].ToString().Trim() : null;
    string? currency = query.ContainsKey("currency") ? query["currency"].ToString() : null;
    string? sort = query.ContainsKey("sort") ? query["sort"].ToString() : null;
    string? category = query.ContainsKey("category") ? query["category"].ToString().Trim() : null;
    string? brand = query.ContainsKey("brand") ? query["brand"].ToString().Trim() : null;
    if (category is not null && (category.Length == 0 || category.Length > 100) || brand is not null && (brand.Length == 0 || brand.Length > 100))
        return Results.Problem(statusCode: 400, title: "Invalid category or brand", detail: "category and brand must contain 1-100 characters.");
    decimal? min = null, max = null;
    foreach (var key in new[] { "search", "currency", "sort", "category", "brand", "minPrice", "maxPrice", "page", "pageSize" })
        if (query[key].Count > 1) return Results.Problem(statusCode: 400, title: "Invalid parameter", detail: key + " must occur once.");
    foreach (var key in new[] { "minPrice", "maxPrice" }) {
        if (!query.ContainsKey(key)) continue;
        if (!decimal.TryParse(query[key], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var value) || value < 0 || value > 9999999999.99m)
            return Results.Problem(statusCode: 400, title: "Invalid price", detail: key + " must be a number between 0 and 9999999999.99 using a decimal point.");
        if (key == "minPrice") min = value; else max = value;
    }
    if (search?.Length > 200 || (currency is not null && currency is not ("TRY" or "USD" or "EUR")) ||
        (sort is not null && sort is not ("name" or "name-desc" or "price" or "price-desc")) || min > max ||
        (currency is null && (min is not null || max is not null || sort is "price" or "price-desc")))
        return Results.Problem(statusCode: 400, title: "Invalid catalog filters", detail: "search: at most 200 characters; currency: TRY/USD/EUR; sort: name/name-desc/price/price-desc. Price filters and sorting require currency; minPrice must not exceed maxPrice.");
    return Results.Ok(await repository.ListAsync(page, size, ct, new(search, currency, min, max, sort, category, brand)));
});
app.MapGet("/products/{id}", async (string id, IProductRepository repository, CancellationToken ct) =>
{
    if (!Guid.TryParse(id, out var key)) return Results.Problem(statusCode: 400, title: "Invalid product id");
    var product = await repository.FindAsync(key, ct);
    return product is null ? Results.Problem(statusCode: 404, title: "Product not found") : Results.Ok(product);
});
app.Run();
public partial class Program { }
