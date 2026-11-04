using Microsoft.EntityFrameworkCore;
namespace Messaging;
public record StockEvent(Guid MessageId, string Type, int Version, Guid OrderId, Guid ProductId, int Quantity, bool Accepted = false, string? Reason = null);
public sealed class Outbox {
 public Guid Id { get; set; }
 public string Payload { get; set; } = "";
 public bool Sent { get; set; }
}
public sealed class Inbox { public Guid Id { get; set; } }
public abstract class MessageDb(DbContextOptions options) : DbContext(options) {
 public DbSet<Outbox> Outbox => Set<Outbox>();
 public DbSet<Inbox> Inbox => Set<Inbox>();
 public void Enqueue(StockEvent message) => Outbox.Add(new() { Id = message.MessageId, Payload = System.Text.Json.JsonSerializer.Serialize(message) });
}
public interface IMessageHandler { Task Handle(StockEvent message, CancellationToken ct); }
