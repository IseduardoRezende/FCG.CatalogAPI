using FCG.Catalog.Application.Services.Interfaces;
using FCG.Contracts.Events;
using MassTransit;

namespace FCG.Catalog.Infrastructure.Messaging.Consumers;

public class PaymentProcessedConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly IOrderService _orderService;

    public PaymentProcessedConsumer(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        return _orderService.ApplyPaymentResultAsync(context.Message, context.CancellationToken);
    }
}
