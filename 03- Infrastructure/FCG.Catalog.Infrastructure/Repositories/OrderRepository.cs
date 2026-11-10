using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Enums;
using FCG.Catalog.Domain.Filters;
using FCG.Catalog.Domain.Repositories;
using FCG.Catalog.Infrastructure.DbContexts;
using FCG.Catalog.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FCG.Catalog.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly CatalogDbContext _dbContext;

    public OrderRepository(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Order>().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsPendingAsync(long userId, long gameId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Order>()
            .AnyAsync(o => o.UserId == userId && o.GameId == gameId && o.Status == OrderStatus.Pending, cancellationToken);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> GetPagedAsync(OrderFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<Order>()
            .Where(ApplyFilter(filter));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplyOrdering(filter)
            .ApplyPagination(filter)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<Order>().AddAsync(order, cancellationToken);
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<Order>().Update(order);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    private static Expression<Func<Order, bool>> ApplyFilter(OrderFilter filter)
    {
        var value = filter.Value?.ToLower();

        return o =>
            (filter.UserId == null || o.UserId == filter.UserId) &&
            (filter.GameId == null || o.GameId == filter.GameId) &&
            (filter.Status == null || o.Status == filter.Status) &&
            (string.IsNullOrWhiteSpace(value) || o.GameName.ToLower().Contains(value));
    }
}
