using FCG.Catalog.Application.Abstractions;
using FCG.Catalog.Application.DTOs.Orders;
using FCG.Catalog.Application.Services;
using FCG.Catalog.Application.Validators;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Enums;
using FCG.Catalog.Domain.Filters;
using FCG.Catalog.Domain.Repositories;
using FCG.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FCG.Catalog.UnitTests.Services;

public class OrderServiceTests
{
    private const long UserId = 1;
    private const string UserEmail = "player@test.com";
    private const long GameId = 10;

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IGameRepository> _gameRepository = new();
    private readonly Mock<IUserGameRepository> _userGameRepository = new();
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _currentUser.Setup(x => x.UserId).Returns(UserId);
        _currentUser.Setup(x => x.Email).Returns(UserEmail);

        _gameRepository.Setup(x => x.GetByIdAsync(GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Game { Id = GameId, Name = "Hollow Knight", Price = 59.90m });

        _orderRepository.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => order.Id = 100)
            .Returns(Task.CompletedTask);

        _orderService = new OrderService(
            _orderRepository.Object,
            _gameRepository.Object,
            _userGameRepository.Object,
            _eventPublisher.Object,
            _currentUser.Object,
            new CreateOrderDtoValidator(),
            NullLogger<OrderService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PaymentProcessedEvent Payment(PaymentStatus status) =>
        new(100, UserId, UserEmail, GameId, "Hollow Knight", 59.90m, status);

    private void SetupExistingOrder(OrderStatus status = OrderStatus.Pending)
    {
        _orderRepository.Setup(x => x.GetByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            Id = 100,
            UserId = UserId,
            UserEmail = UserEmail,
            GameId = GameId,
            GameName = "Hollow Knight",
            Price = 59.90m,
            Status = status
        });
    }

    private void VerifyNoOrderPlacedEventPublished() =>
        _eventPublisher.Verify(x => x.PublishAsync(It.IsAny<OrderPlacedEvent>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task PlaceAsync_WithAvailableGame_CreatesPendingOrderWithGameNameAndPriceSnapshot()
    {
        Order? persisted = null;
        _orderRepository.Setup(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => persisted = order)
            .Returns(Task.CompletedTask);

        var result = await _orderService.PlaceAsync(new CreateOrderDto { GameId = GameId }, Ct);

        Assert.IsType<SuccessResult<ReadOrderDto>>(result);
        Assert.Equal(OrderStatus.Pending, persisted!.Status);
        Assert.Equal(UserId, persisted.UserId);
        Assert.Equal(UserEmail, persisted.UserEmail);
        Assert.Equal("Hollow Knight", persisted.GameName);
        Assert.Equal(59.90m, persisted.Price);
    }

    [Fact]
    public async Task PlaceAsync_WithAvailableGame_PublishesOrderPlacedEventWithOrderData()
    {
        await _orderService.PlaceAsync(new CreateOrderDto { GameId = GameId }, Ct);

        _eventPublisher.Verify(x => x.PublishAsync(
            It.Is<OrderPlacedEvent>(e =>
                e.OrderId == 100 &&
                e.UserId == UserId &&
                e.UserEmail == UserEmail &&
                e.GameId == GameId &&
                e.GameName == "Hollow Knight" &&
                e.Price == 59.90m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceAsync_WithAvailableGame_PersistsOrderBeforePublishingEvent()
    {
        var calls = new List<string>();
        _orderRepository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save")).Returns(Task.CompletedTask);
        _eventPublisher.Setup(x => x.PublishAsync(It.IsAny<OrderPlacedEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("publish")).Returns(Task.CompletedTask);

        await _orderService.PlaceAsync(new CreateOrderDto { GameId = GameId }, Ct);

        Assert.Equal(["save", "publish"], calls);
    }

    [Fact]
    public async Task PlaceAsync_WithNonexistentGame_ReturnsNotFoundAndDoesNotPublish()
    {
        var result = await _orderService.PlaceAsync(new CreateOrderDto { GameId = 999 }, Ct);

        Assert.IsType<NotFoundResult<ReadOrderDto>>(result);
        VerifyNoOrderPlacedEventPublished();
    }

    [Fact]
    public async Task PlaceAsync_WithGameAlreadyInLibrary_ReturnsConflictAndDoesNotPublish()
    {
        _userGameRepository.Setup(x => x.ExistsAsync(UserId, GameId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _orderService.PlaceAsync(new CreateOrderDto { GameId = GameId }, Ct);

        Assert.IsType<ConflictResult<ReadOrderDto>>(result);
        _orderRepository.Verify(x => x.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoOrderPlacedEventPublished();
    }

    [Fact]
    public async Task PlaceAsync_WithPendingOrderForSameGame_ReturnsConflictAndDoesNotPublish()
    {
        _orderRepository.Setup(x => x.ExistsPendingAsync(UserId, GameId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _orderService.PlaceAsync(new CreateOrderDto { GameId = GameId }, Ct);

        Assert.IsType<ConflictResult<ReadOrderDto>>(result);
        VerifyNoOrderPlacedEventPublished();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PlaceAsync_WithInvalidGameId_ReturnsInvalid(long gameId)
    {
        var result = await _orderService.PlaceAsync(new CreateOrderDto { GameId = gameId }, Ct);

        Assert.IsType<InvalidResult<ReadOrderDto>>(result);
        VerifyNoOrderPlacedEventPublished();
    }

    [Fact]
    public async Task PlaceAsync_WithTokenWithoutEmail_ReturnsInvalidBecauseNotificationNeedsRecipient()
    {
        _currentUser.Setup(x => x.Email).Returns((string?)null);

        var result = await _orderService.PlaceAsync(new CreateOrderDto { GameId = GameId }, Ct);

        Assert.IsType<InvalidResult<ReadOrderDto>>(result);
        VerifyNoOrderPlacedEventPublished();
    }

    [Fact]
    public async Task ApplyPaymentResultAsync_WithApprovedPayment_MarksOrderApprovedAndAddsGameToLibrary()
    {
        SetupExistingOrder();
        Order? updated = null;
        UserGame? added = null;
        _orderRepository.Setup(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => updated = order).Returns(Task.CompletedTask);
        _userGameRepository.Setup(x => x.AddAsync(It.IsAny<UserGame>(), It.IsAny<CancellationToken>()))
            .Callback<UserGame, CancellationToken>((userGame, _) => added = userGame).Returns(Task.CompletedTask);

        var applied = await _orderService.ApplyPaymentResultAsync(Payment(PaymentStatus.Approved), Ct);

        Assert.True(applied);
        Assert.Equal(OrderStatus.Approved, updated!.Status);
        Assert.Equal(UserId, added!.UserId);
        Assert.Equal(GameId, added.GameId);
        Assert.Equal(100, added.OrderId);
        Assert.Equal(59.90m, added.PricePaid);
        _orderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyPaymentResultAsync_WithRejectedPayment_MarksOrderRejectedAndDoesNotAddToLibrary()
    {
        SetupExistingOrder();
        Order? updated = null;
        _orderRepository.Setup(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => updated = order).Returns(Task.CompletedTask);

        var applied = await _orderService.ApplyPaymentResultAsync(Payment(PaymentStatus.Rejected), Ct);

        Assert.True(applied);
        Assert.Equal(OrderStatus.Rejected, updated!.Status);
        _userGameRepository.Verify(x => x.AddAsync(It.IsAny<UserGame>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(OrderStatus.Approved)]
    [InlineData(OrderStatus.Rejected)]
    public async Task ApplyPaymentResultAsync_WhenOrderWasAlreadyProcessed_IgnoresDuplicatedEvent(OrderStatus currentStatus)
    {
        SetupExistingOrder(currentStatus);

        var applied = await _orderService.ApplyPaymentResultAsync(Payment(PaymentStatus.Approved), Ct);

        Assert.False(applied);
        _orderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        _userGameRepository.Verify(x => x.AddAsync(It.IsAny<UserGame>(), It.IsAny<CancellationToken>()), Times.Never);
        _orderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyPaymentResultAsync_WhenOrderDoesNotExist_ReturnsFalseWithoutSaving()
    {
        var applied = await _orderService.ApplyPaymentResultAsync(Payment(PaymentStatus.Approved), Ct);

        Assert.False(applied);
        _orderRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyPaymentResultAsync_ApprovedWhenGameAlreadyInLibrary_DoesNotDuplicateLibraryEntry()
    {
        SetupExistingOrder();
        _userGameRepository.Setup(x => x.ExistsAsync(UserId, GameId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var applied = await _orderService.ApplyPaymentResultAsync(Payment(PaymentStatus.Approved), Ct);

        Assert.True(applied);
        _userGameRepository.Verify(x => x.AddAsync(It.IsAny<UserGame>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ForOrderOfAnotherUser_ReturnsForbidden()
    {
        _currentUser.Setup(x => x.UserId).Returns(2);
        SetupExistingOrder();

        var result = await _orderService.GetByIdAsync(100, Ct);

        Assert.IsType<ForbiddenResult<ReadOrderDto>>(result);
    }

    [Fact]
    public async Task GetByIdAsync_AsAdministrator_ReturnsOrderOfAnyUser()
    {
        _currentUser.Setup(x => x.UserId).Returns(2);
        _currentUser.Setup(x => x.IsAdministrator).Returns(true);
        SetupExistingOrder();

        var result = await _orderService.GetByIdAsync(100, Ct);

        Assert.IsType<SuccessResult<ReadOrderDto>>(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        var result = await _orderService.GetByIdAsync(999, Ct);

        Assert.IsType<NotFoundResult<ReadOrderDto>>(result);
    }

    [Fact]
    public async Task GetPagedAsync_AsCommonUserWithoutFilter_ListsOnlyOwnOrders()
    {
        OrderFilter? used = null;
        _orderRepository.Setup(x => x.GetPagedAsync(It.IsAny<OrderFilter>(), It.IsAny<CancellationToken>()))
            .Callback<OrderFilter, CancellationToken>((filter, _) => used = filter)
            .ReturnsAsync(([], 0));

        var result = await _orderService.GetPagedAsync(new OrderFilter(), Ct);

        Assert.IsType<SuccessResult<Pagination<ReadOrderDto>>>(result);
        Assert.Equal(UserId, used!.UserId);
    }

    [Fact]
    public async Task GetPagedAsync_AsCommonUserFilteringAnotherUser_ReturnsForbidden()
    {
        var result = await _orderService.GetPagedAsync(new OrderFilter { UserId = 2 }, Ct);

        Assert.IsType<ForbiddenResult<Pagination<ReadOrderDto>>>(result);
    }
}
