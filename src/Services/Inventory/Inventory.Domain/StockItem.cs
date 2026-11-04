namespace Inventory.Domain;

public sealed class StockItem
{
    private StockItem() { }
    public StockItem(Guid id, int quantity)
    {
        if (id == Guid.Empty || quantity < 0) throw new ArgumentException("Valid product id and nonnegative quantity required.");
        Id = id; Quantity = quantity;
    }
    public Guid Id { get; private set; }
    public int Quantity { get; private set; }
    public int Reserved { get; private set; }
    public int Available => Quantity - Reserved;
}

public sealed class Reservation
{
    private Reservation() { }
    public Reservation(Guid id, Guid productId, int quantity)
    {
        if (id == Guid.Empty || productId == Guid.Empty || quantity <= 0) throw new ArgumentException("Valid ids and positive quantity required.");
        Id = id; ProductId = productId; Quantity = quantity;
    }
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public string Status { get; private set; } = "Reserved";
    public void Complete(bool commit)
    {
        var target = commit ? "Committed" : "Released";
        if (Status == target) return;
        if (Status != "Reserved") throw new InvalidOperationException("Reservation already finalized differently.");
        Status = target;
    }
}
