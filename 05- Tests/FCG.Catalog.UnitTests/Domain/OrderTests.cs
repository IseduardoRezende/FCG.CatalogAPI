using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Enums;

namespace FCG.Catalog.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void NewOrder_ByDefault_StartsAsPending()
    {
        var order = new Order();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.True(order.IsPending);
        Assert.Null(order.ProcessedAt);
    }

    [Fact]
    public void ApplyPaymentResult_OnPendingOrderWithApprovedPayment_MarksAsApprovedWithProcessingDate()
    {
        var order = new Order();
        var processedAt = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        var applied = order.ApplyPaymentResult(approved: true, processedAt);

        Assert.True(applied);
        Assert.Equal(OrderStatus.Approved, order.Status);
        Assert.Equal(processedAt, order.ProcessedAt);
    }

    [Fact]
    public void ApplyPaymentResult_OnPendingOrderWithRejectedPayment_MarksAsRejected()
    {
        var order = new Order();

        var applied = order.ApplyPaymentResult(approved: false, DateTime.UtcNow);

        Assert.True(applied);
        Assert.Equal(OrderStatus.Rejected, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Approved, false)]
    [InlineData(OrderStatus.Rejected, true)]
    public void ApplyPaymentResult_OnAlreadyProcessedOrder_KeepsOriginalStatus(OrderStatus currentStatus, bool approved)
    {
        var order = new Order { Status = currentStatus };

        var applied = order.ApplyPaymentResult(approved, DateTime.UtcNow);

        Assert.False(applied);
        Assert.Equal(currentStatus, order.Status);
    }
}
