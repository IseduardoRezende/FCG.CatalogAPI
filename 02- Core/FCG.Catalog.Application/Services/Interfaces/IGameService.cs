using FCG.Catalog.Application.DTOs.Games;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Filters;

namespace FCG.Catalog.Application.Services.Interfaces;

public interface IGameService
{
    Task<Result<ReadGameDto>> CreateAsync(CreateGameDto dto, CancellationToken cancellationToken = default);

    Task<Result<Pagination<ReadGameDto>>> GetPagedAsync(GameFilter filter, CancellationToken cancellationToken = default);

    Task<Result<ReadGameDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<ReadGameDto>> UpdateAsync(long id, UpdateGameDto dto, CancellationToken cancellationToken = default);

    Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
