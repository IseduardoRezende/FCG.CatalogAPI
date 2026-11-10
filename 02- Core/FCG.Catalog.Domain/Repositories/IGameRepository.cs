using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Filters;

namespace FCG.Catalog.Domain.Repositories;

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Game> Items, int TotalCount)> GetPagedAsync(GameFilter filter, CancellationToken cancellationToken = default);

    Task AddAsync(Game game, CancellationToken cancellationToken = default);

    Task UpdateAsync(Game game, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
