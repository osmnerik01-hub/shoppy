namespace Orders.Domain;
public sealed class Order {
 public Guid Id { get; set; } = Guid.NewGuid();
 public string IdempotencyKey { get; set; } = "";
 public Guid ProductId { get; set; }
 public int Quantity { get; set; }
 public decimal UnitPrice { get; set; }
 public string Currency { get; set; } = "";
 public string Status { get; set; } = "Pending";
 public string? RejectionReason { get; set; }
}
