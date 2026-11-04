using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
namespace Messaging;
public sealed class BrokerWorker<T>(IServiceScopeFactory scopes, string uri, string queue, ILogger<BrokerWorker<T>> log) : BackgroundService where T : MessageDb {
 protected override async Task ExecuteAsync(CancellationToken ct) {
  while (!ct.IsCancellationRequested) {
   try {
    var factory = new ConnectionFactory { Uri = new Uri(uri) };
    await using var connection = await factory.CreateConnectionAsync(ct);
    await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
    await channel.ExchangeDeclareAsync("commerce.v1", ExchangeType.Direct, durable: true, cancellationToken: ct);
    foreach (var route in new[] { ("orders.results.v1", "stock.completed.v1"), ("inventory.orders.v1", "order.placed.v1") }) {
     await channel.QueueDeclareAsync(route.Item1, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
     await channel.QueueBindAsync(route.Item1, "commerce.v1", route.Item2, cancellationToken: ct);
    }
    while (!ct.IsCancellationRequested) {
     using var scope = scopes.CreateScope();
     var db = scope.ServiceProvider.GetRequiredService<T>();
     await OutboxDispatch.Send(db, async payload => {
      var message = JsonSerializer.Deserialize<StockEvent>(payload)!;
      await channel.BasicPublishAsync("commerce.v1", message.Type, mandatory: true, new BasicProperties { Persistent = true, MessageId = message.MessageId.ToString(), ContentType = "application/json" }, Encoding.UTF8.GetBytes(payload), ct);
     }, ct);
     var delivery = await channel.BasicGetAsync(queue, autoAck: false, ct);
     if (delivery is not null) {
      var message = JsonSerializer.Deserialize<StockEvent>(delivery.Body.Span) ?? throw new InvalidOperationException("Empty event");
      await scope.ServiceProvider.GetRequiredService<IMessageHandler>().Handle(message, ct);
      await channel.BasicAckAsync(delivery.DeliveryTag, false, ct);
     } else await Task.Delay(200, ct);
    }
   } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
   catch (Exception ex) { log.LogWarning(ex, "Messaging interrupted; durable work will be retried"); await Task.Delay(1000, ct); }
  }
 }
}

