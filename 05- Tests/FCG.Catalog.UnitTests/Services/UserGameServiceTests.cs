using FCG.Catalog.Application.Abstractions;
using FCG.Catalog.Application.DTOs.UserGames;
using FCG.Catalog.Application.Services;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Filters;
using FCG.Catalog.Domain.Repositories;
using Moq;

namespace FCG.Catalog.UnitTests.Services;

public class UserGameServiceTests
{
    private readonly Mock<IUserGameRepository> _userGameRepository = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly UserGameService _userGameService;
    private UserGameFilter? _usedFilter;

    public UserGameServiceTests()
    {
        _currentUser.Setup(x => x.UserId).Returns(1);

        _userGameRepository.Setup(x => x.GetPagedAsync(It.IsAny<UserGameFilter>(), It.IsAny<CancellationToken>()))
            .Callback<UserGameFilter, CancellationToken>((filter, _) => _usedFilter = filter)
            .ReturnsAsync(([], 0));

        _userGameRepository.Setup(x => x.GetByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserGame { Id = 50, UserId = 1, GameId = 10, GameName = "Celeste" });

        _userGameService = new UserGameService(_userGameRepository.Object, _currentUser.Object);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetLibraryAsync_AsCommonUserWithoutFilter_ReturnsOnlyOwnLibrary()
    {
        var result = await _userGameService.GetLibraryAsync(new UserGameFilter(), Ct);

        Assert.IsType<SuccessResult<Pagination<ReadUserGameDto>>>(result);
        Assert.Equal(1, _usedFilter!.UserId);
    }

    [Fact]
    public async Task GetLibraryAsync_AsCommonUserRequestingAnotherUsersLibrary_ReturnsForbidden()
    {
        var result = await _userGameService.GetLibraryAsync(new UserGameFilter { UserId = 2 }, Ct);

        Assert.IsType<ForbiddenResult<Pagination<ReadUserGameDto>>>(result);
        _userGameRepository.Verify(x => x.GetPagedAsync(It.IsAny<UserGameFilter>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLibraryAsync_AsAdministratorWithoutFilter_ListsAllLibraries()
    {
        _currentUser.Setup(x => x.IsAdministrator).Returns(true);

        await _userGameService.GetLibraryAsync(new UserGameFilter(), Ct);

        Assert.Null(_usedFilter!.UserId);
    }

    [Fact]
    public async Task GetLibraryAsync_AsAdministratorFilteringByUser_ListsThatUsersLibrary()
    {
        _currentUser.Setup(x => x.IsAdministrator).Returns(true);

        await _userGameService.GetLibraryAsync(new UserGameFilter { UserId = 2 }, Ct);

        Assert.Equal(2, _usedFilter!.UserId);
    }

    [Fact]
    public async Task GetByIdAsync_ForOwnPurchase_ReturnsPurchase()
    {
        var result = await _userGameService.GetByIdAsync(50, Ct);

        Assert.IsType<SuccessResult<ReadUserGameDto>>(result);
        Assert.Equal("Celeste", result.Value!.GameName);
    }

    [Fact]
    public async Task GetByIdAsync_ForPurchaseOfAnotherUser_ReturnsForbidden()
    {
        _currentUser.Setup(x => x.UserId).Returns(2);

        var result = await _userGameService.GetByIdAsync(50, Ct);

        Assert.IsType<ForbiddenResult<ReadUserGameDto>>(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenPurchaseDoesNotExist_ReturnsNotFound()
    {
        var result = await _userGameService.GetByIdAsync(999, Ct);

        Assert.IsType<NotFoundResult<ReadUserGameDto>>(result);
    }
}
