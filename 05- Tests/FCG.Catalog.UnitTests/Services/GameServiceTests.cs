using FCG.Catalog.Application.DTOs.Games;
using FCG.Catalog.Application.Services;
using FCG.Catalog.Application.Validators;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Repositories;
using Moq;

namespace FCG.Catalog.UnitTests.Services;

public class GameServiceTests
{
    private readonly Mock<IGameRepository> _gameRepository = new();
    private readonly GameService _gameService;

    public GameServiceTests()
    {
        _gameService = new GameService(_gameRepository.Object, new CreateGameDtoValidator(), new UpdateGameDtoValidator());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CreateGameDto ValidCreateDto() => new() { Name = "Celeste", Description = "Platformer", Price = 39.90m };

    private void SetupExistingGame() =>
        _gameRepository.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Game { Id = 1, Name = "Celeste", Description = "Platformer", Price = 39.90m });

    [Fact]
    public async Task CreateAsync_WithValidData_PersistsTrimmedGame()
    {
        Game? persisted = null;
        _gameRepository.Setup(x => x.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<Game, CancellationToken>((game, _) => persisted = game).Returns(Task.CompletedTask);

        var result = await _gameService.CreateAsync(new CreateGameDto { Name = "  Celeste ", Description = " Platformer ", Price = 39.90m }, Ct);

        Assert.IsType<SuccessResult<ReadGameDto>>(result);
        Assert.Equal("Celeste", persisted!.Name);
        Assert.Equal("Platformer", persisted.Description);
        Assert.Equal(39.90m, persisted.Price);
        _gameRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateAsync_WithNonPositivePrice_ReturnsInvalidAndDoesNotPersist(decimal price)
    {
        var dto = ValidCreateDto();
        dto.Price = price;

        var result = await _gameService.CreateAsync(dto, Ct);

        Assert.IsType<InvalidResult<ReadGameDto>>(result);
        _gameRepository.Verify(x => x.AddAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ReturnsInvalid()
    {
        var dto = ValidCreateDto();
        dto.Name = "";

        var result = await _gameService.CreateAsync(dto, Ct);

        Assert.IsType<InvalidResult<ReadGameDto>>(result);
    }

    [Fact]
    public async Task CreateAsync_WithDescriptionLongerThan500Characters_ReturnsInvalid()
    {
        var dto = ValidCreateDto();
        dto.Description = new string('d', 501);

        var result = await _gameService.CreateAsync(dto, Ct);

        Assert.IsType<InvalidResult<ReadGameDto>>(result);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesNameDescriptionAndPrice()
    {
        SetupExistingGame();
        Game? updated = null;
        _gameRepository.Setup(x => x.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<Game, CancellationToken>((game, _) => updated = game).Returns(Task.CompletedTask);

        var result = await _gameService.UpdateAsync(1, new UpdateGameDto { Name = "Celeste DX", Description = "Remaster", Price = 49.90m }, Ct);

        Assert.IsType<SuccessResult<ReadGameDto>>(result);
        Assert.Equal("Celeste DX", updated!.Name);
        Assert.Equal(49.90m, updated.Price);
    }

    [Fact]
    public async Task UpdateAsync_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = await _gameService.UpdateAsync(999, new UpdateGameDto { Name = "X", Description = "Y", Price = 1 }, Ct);

        Assert.IsType<NotFoundResult<ReadGameDto>>(result);
    }

    [Fact]
    public async Task DeleteAsync_WhenGameExists_MarksGameAsDeleted()
    {
        SetupExistingGame();
        Game? updated = null;
        _gameRepository.Setup(x => x.UpdateAsync(It.IsAny<Game>(), It.IsAny<CancellationToken>()))
            .Callback<Game, CancellationToken>((game, _) => updated = game).Returns(Task.CompletedTask);

        var result = await _gameService.DeleteAsync(1, Ct);

        Assert.IsType<SuccessResult<bool>>(result);
        Assert.True(updated!.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = await _gameService.DeleteAsync(999, Ct);

        Assert.IsType<NotFoundResult<bool>>(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var result = await _gameService.GetByIdAsync(999, Ct);

        Assert.IsType<NotFoundResult<ReadGameDto>>(result);
    }
}
