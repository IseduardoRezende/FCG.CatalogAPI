using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Filters;

namespace FCG.Catalog.Domain.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> ExistsPendingAsync(long userId, long gameId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Order> Items, int TotalCount)> GetPagedAsync(OrderFilter filter, CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
