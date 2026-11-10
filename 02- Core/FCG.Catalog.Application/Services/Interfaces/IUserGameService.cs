using FCG.Catalog.Application.DTOs.UserGames;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Filters;

namespace FCG.Catalog.Application.Services.Interfaces;

public interface IUserGameService
{
    Task<Result<Pagination<ReadUserGameDto>>> GetLibraryAsync(UserGameFilter filter, CancellationToken cancellationToken = default);

    Task<Result<ReadUserGameDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
