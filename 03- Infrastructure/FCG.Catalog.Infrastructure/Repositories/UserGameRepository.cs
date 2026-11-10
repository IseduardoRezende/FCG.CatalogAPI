using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Filters;
using FCG.Catalog.Domain.Repositories;
using FCG.Catalog.Infrastructure.DbContexts;
using FCG.Catalog.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FCG.Catalog.Infrastructure.Repositories;

public class UserGameRepository : IUserGameRepository
{
    private readonly CatalogDbContext _dbContext;

    public UserGameRepository(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserGame?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<UserGame>().FirstOrDefaultAsync(ug => ug.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(long userId, long gameId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<UserGame>()
            .AnyAsync(ug => ug.UserId == userId && ug.GameId == gameId, cancellationToken);
    }

    public async Task<(IReadOnlyList<UserGame> Items, int TotalCount)> GetPagedAsync(UserGameFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Set<UserGame>()
            .Where(ApplyFilter(filter));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplyOrdering(filter)
            .ApplyPagination(filter)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(UserGame userGame, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<UserGame>().AddAsync(userGame, cancellationToken);
    }

    private static Expression<Func<UserGame, bool>> ApplyFilter(UserGameFilter filter)
    {
        var value = filter.Value?.ToLower();

        return ug =>
            (filter.UserId == null || ug.UserId == filter.UserId) &&
            (filter.GameId == null || ug.GameId == filter.GameId) &&
            (filter.PurchasedFrom == null || ug.PurchasedAt >= filter.PurchasedFrom) &&
            (filter.PurchasedTo == null || ug.PurchasedAt <= filter.PurchasedTo) &&
            (string.IsNullOrWhiteSpace(value) || ug.GameName.ToLower().Contains(value));
    }
}
