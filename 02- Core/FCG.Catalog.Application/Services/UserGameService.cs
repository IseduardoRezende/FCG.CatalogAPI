using FCG.Catalog.Application.Abstractions;
using FCG.Catalog.Application.DTOs.UserGames;
using FCG.Catalog.Application.Services.Interfaces;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Filters;
using FCG.Catalog.Domain.Repositories;

namespace FCG.Catalog.Application.Services;

public class UserGameService : IUserGameService
{
    private readonly IUserGameRepository _userGameRepository;
    private readonly ICurrentUser _currentUser;

    public UserGameService(IUserGameRepository userGameRepository, ICurrentUser currentUser)
    {
        _userGameRepository = userGameRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<Pagination<ReadUserGameDto>>> GetLibraryAsync(UserGameFilter filter, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAdministrator)
        {
            if (filter.UserId.HasValue && filter.UserId != _currentUser.UserId)
                return ForbiddenResult<Pagination<ReadUserGameDto>>.Create(new Error("You can only access your own library."));

            filter.UserId = _currentUser.UserId;
        }

        var (items, totalCount) = await _userGameRepository.GetPagedAsync(filter, cancellationToken);
        var mapped = items.Select(MapToReadDto).ToList();

        return SuccessResult<Pagination<ReadUserGameDto>>.Create(
            new Pagination<ReadUserGameDto>(mapped, totalCount, filter.CurrentPage, filter.PageSize));
    }

    public async Task<Result<ReadUserGameDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var userGame = await _userGameRepository.GetByIdAsync(id, cancellationToken);

        if (userGame is null)
            return NotFoundResult<ReadUserGameDto>.Create(new Error("Purchase not found."));

        if (userGame.UserId != _currentUser.UserId && !_currentUser.IsAdministrator)
            return ForbiddenResult<ReadUserGameDto>.Create(new Error("You can only access your own library."));

        return SuccessResult<ReadUserGameDto>.Create(MapToReadDto(userGame));
    }

    private static ReadUserGameDto MapToReadDto(UserGame userGame)
    {
        return new ReadUserGameDto
        {
            Id = userGame.Id,
            UserId = userGame.UserId,
            UserEmail = userGame.UserEmail,
            GameId = userGame.GameId,
            GameName = userGame.GameName,
            PricePaid = userGame.PricePaid,
            OrderId = userGame.OrderId,
            PurchasedAt = userGame.PurchasedAt
        };
    }
}
