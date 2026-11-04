using Inventory.Domain;
using Xunit;
public class ReservationTests
{
    [Fact]
    public void ValidatesQuantitiesAndIdentity()
    {
        Assert.Throws<ArgumentException>(() => new StockItem(Guid.NewGuid(), -1));
        Assert.Throws<ArgumentException>(() => new StockItem(Guid.Empty, 1));
        Assert.Throws<ArgumentException>(() => new Reservation(Guid.NewGuid(), Guid.NewGuid(), 0));
        Assert.Throws<ArgumentException>(() => new Reservation(Guid.Empty, Guid.NewGuid(), 1));
    }
    [Theory]
    [InlineData(true, "Committed")]
    [InlineData(false, "Released")]
    public void TerminalStateIsIdempotentAndCannotBeReversed(bool commit, string status)
    {
        var r = new Reservation(Guid.NewGuid(), Guid.NewGuid(), 1);
        r.Complete(commit); r.Complete(commit);
        Assert.Equal(status, r.Status);
        Assert.Throws<InvalidOperationException>(() => r.Complete(!commit));
    }
}
