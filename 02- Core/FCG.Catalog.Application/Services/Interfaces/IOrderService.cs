using FCG.Catalog.Application.DTOs.Orders;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Filters;
using FCG.Contracts.Events;

namespace FCG.Catalog.Application.Services.Interfaces;

public interface IOrderService
{
    Task<Result<ReadOrderDto>> PlaceAsync(CreateOrderDto dto, CancellationToken cancellationToken = default);

    Task<Result<ReadOrderDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<Pagination<ReadOrderDto>>> GetPagedAsync(OrderFilter filter, CancellationToken cancellationToken = default);

    Task<bool> ApplyPaymentResultAsync(PaymentProcessedEvent payment, CancellationToken cancellationToken = default);
}
