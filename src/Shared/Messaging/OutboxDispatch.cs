using Microsoft.EntityFrameworkCore;
namespace Messaging;
public static class OutboxDispatch {
 public static async Task Send(MessageDb db, Func<string, Task> publish, CancellationToken ct = default) {
  foreach (var row in await db.Outbox.Where(x => !x.Sent).Take(100).ToListAsync(ct)) {
   await publish(row.Payload); // consumer transaction must complete before recording delivery
   row.Sent = true;
   await db.SaveChangesAsync(ct);
  }
 }
}
