using System.Text.Json;
using Messaging;
using Xunit;
public sealed class EventTests {
 [Theory]
 [InlineData("order.placed.v1")]
 [InlineData("stock.completed.v1")]
 public void Serialized_wire_matches_versioned_schema(string type) {
  using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "stock-workflow.v1.schema.json")));
  var m = new StockEvent(Guid.NewGuid(), type, 1, Guid.NewGuid(), Guid.NewGuid(), 2, true);
  using var wire = JsonDocument.Parse(JsonSerializer.Serialize(m));
  Assert.Equal(schema.RootElement.GetProperty("required").EnumerateArray().Select(x => x.GetString()).Order(), wire.RootElement.EnumerateObject().Select(x => x.Name).Order());
  Assert.Contains(type, schema.RootElement.GetProperty("properties").GetProperty("Type").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
  Assert.Equal(schema.RootElement.GetProperty("properties").GetProperty("Version").GetProperty("const").GetInt32(), wire.RootElement.GetProperty("Version").GetInt32());
  Assert.Equal(m, JsonSerializer.Deserialize<StockEvent>(wire.RootElement.GetRawText()));
 }
}
