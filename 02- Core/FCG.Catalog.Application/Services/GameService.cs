using FCG.Catalog.Application.DTOs.Games;
using FCG.Catalog.Application.Extensions;
using FCG.Catalog.Application.Services.Interfaces;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Filters;
using FCG.Catalog.Domain.Repositories;
using FluentValidation;

namespace FCG.Catalog.Application.Services;

public class GameService : IGameService
{
    private readonly IGameRepository _gameRepository;
    private readonly IValidator<CreateGameDto> _createValidator;
    private readonly IValidator<UpdateGameDto> _updateValidator;

    public GameService(
        IGameRepository gameRepository,
        IValidator<CreateGameDto> createValidator,
        IValidator<UpdateGameDto> updateValidator)
    {
        _gameRepository = gameRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<Result<ReadGameDto>> CreateAsync(CreateGameDto dto, CancellationToken cancellationToken = default)
    {
        var validation = (await _createValidator.ValidateAsync(dto, cancellationToken)).ToInvalidResult<ReadGameDto>();

        if (validation is not null)
            return validation;

        var game = new Game
        {
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            Price = dto.Price,
            CreatedAt = DateTime.UtcNow
        };

        await _gameRepository.AddAsync(game, cancellationToken);
        await _gameRepository.SaveChangesAsync(cancellationToken);

        return SuccessResult<ReadGameDto>.Create(MapToReadDto(game));
    }

    public async Task<Result<Pagination<ReadGameDto>>> GetPagedAsync(GameFilter filter, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _gameRepository.GetPagedAsync(filter, cancellationToken);
        var mapped = items.Select(MapToReadDto).ToList();

        return SuccessResult<Pagination<ReadGameDto>>.Create(
            new Pagination<ReadGameDto>(mapped, totalCount, filter.CurrentPage, filter.PageSize));
    }

    public async Task<Result<ReadGameDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(id, cancellationToken);

        if (game is null)
            return NotFoundResult<ReadGameDto>.Create(new Error("Game not found."));

        return SuccessResult<ReadGameDto>.Create(MapToReadDto(game));
    }

    public async Task<Result<ReadGameDto>> UpdateAsync(long id, UpdateGameDto dto, CancellationToken cancellationToken = default)
    {
        var validation = (await _updateValidator.ValidateAsync(dto, cancellationToken)).ToInvalidResult<ReadGameDto>();

        if (validation is not null)
            return validation;

        var game = await _gameRepository.GetByIdAsync(id, cancellationToken);

        if (game is null)
            return NotFoundResult<ReadGameDto>.Create(new Error("Game not found."));

        game.Name = dto.Name.Trim();
        game.Description = dto.Description.Trim();
        game.Price = dto.Price;

        await _gameRepository.UpdateAsync(game, cancellationToken);
        await _gameRepository.SaveChangesAsync(cancellationToken);

        return SuccessResult<ReadGameDto>.Create(MapToReadDto(game));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(id, cancellationToken);

        if (game is null)
            return NotFoundResult<bool>.Create(new Error("Game not found."));

        game.Delete();
        await _gameRepository.UpdateAsync(game, cancellationToken);
        await _gameRepository.SaveChangesAsync(cancellationToken);

        return SuccessResult<bool>.Create(true);
    }

    private static ReadGameDto MapToReadDto(Game game)
    {
        return new ReadGameDto
        {
            Id = game.Id,
            Name = game.Name,
            Description = game.Description,
            Price = game.Price,
            CreatedAt = game.CreatedAt
        };
    }
}
