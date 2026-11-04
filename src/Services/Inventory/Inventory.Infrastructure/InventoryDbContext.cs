using Inventory.Domain;
using Microsoft.EntityFrameworkCore;
namespace Inventory.Infrastructure;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : Messaging.MessageDb(options)
{
    public DbSet<OrderDecision> OrderDecisions => Set<OrderDecision>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<StockItem>(e => {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Available);
            e.ToTable("StockItems", t => t.HasCheckConstraint("CK_Stock", "Quantity >= 0 AND Reserved >= 0 AND Reserved <= Quantity"));
        });
        model.Entity<Reservation>(e => {
            e.HasKey(x => x.Id);
            e.HasOne<StockItem>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).IsRequired();
            e.ToTable("Reservations", t => {
                t.HasCheckConstraint("CK_ReservationQuantity", "Quantity > 0");
                t.HasCheckConstraint("CK_ReservationStatus", "Status IN ('Reserved', 'Committed', 'Released')");
            });
        });
    }
}

public sealed class OrderDecision { public Guid Id { get; set; } public bool Accepted { get; set; } }
