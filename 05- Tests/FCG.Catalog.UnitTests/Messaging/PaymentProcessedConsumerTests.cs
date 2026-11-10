using FCG.Catalog.Application.Services.Interfaces;
using FCG.Catalog.Infrastructure.Messaging.Consumers;
using FCG.Contracts.Events;
using MassTransit;
using Moq;

namespace FCG.Catalog.UnitTests.Messaging;

public class PaymentProcessedConsumerTests
{
    [Fact]
    public async Task Consume_WithPaymentProcessedEvent_DelegatesToOrderService()
    {
        var orderService = new Mock<IOrderService>();
        var message = new PaymentProcessedEvent(100, 1, "player@test.com", 10, "Celeste", 39.90m, PaymentStatus.Approved);
        var context = new Mock<ConsumeContext<PaymentProcessedEvent>>();
        context.Setup(x => x.Message).Returns(message);
        context.Setup(x => x.CancellationToken).Returns(TestContext.Current.CancellationToken);

        await new PaymentProcessedConsumer(orderService.Object).Consume(context.Object);

        orderService.Verify(x => x.ApplyPaymentResultAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }
}
