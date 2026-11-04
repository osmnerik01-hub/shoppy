using Orders.Domain;
using Microsoft.EntityFrameworkCore;
namespace Orders.Infrastructure;
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : Messaging.MessageDb(options) {
 public DbSet<Order> Orders => Set<Order>();
 protected override void OnModelCreating(ModelBuilder model) { model.Entity<Order>().HasIndex(x => x.IdempotencyKey).IsUnique(); }
}
