using FCG.Catalog.Application.Abstractions;
using FCG.Catalog.Application.DTOs.Orders;
using FCG.Catalog.Application.Extensions;
using FCG.Catalog.Application.Services.Interfaces;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Entities;
using FCG.Catalog.Domain.Filters;
using FCG.Catalog.Domain.Repositories;
using FCG.Contracts.Events;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FCG.Catalog.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUserGameRepository _userGameRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateOrderDto> _validator;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IGameRepository gameRepository,
        IUserGameRepository userGameRepository,
        IEventPublisher eventPublisher,
        ICurrentUser currentUser,
        IValidator<CreateOrderDto> validator,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _gameRepository = gameRepository;
        _userGameRepository = userGameRepository;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<ReadOrderDto>> PlaceAsync(CreateOrderDto dto, CancellationToken cancellationToken = default)
    {
        var validation = (await _validator.ValidateAsync(dto, cancellationToken)).ToInvalidResult<ReadOrderDto>();

        if (validation is not null)
            return validation;

        if (_currentUser.UserId is not { } userId || string.IsNullOrWhiteSpace(_currentUser.Email))
            return InvalidResult<ReadOrderDto>.Create(new Error("Authenticated user id and email are required."));

        var game = await _gameRepository.GetByIdAsync(dto.GameId, cancellationToken);

        if (game is null)
            return NotFoundResult<ReadOrderDto>.Create(new Error("Game not found.", nameof(CreateOrderDto.GameId)));

        if (await _userGameRepository.ExistsAsync(userId, game.Id, cancellationToken))
            return ConflictResult<ReadOrderDto>.Create(new Error("Game already in your library.", nameof(CreateOrderDto.GameId)));

        if (await _orderRepository.ExistsPendingAsync(userId, game.Id, cancellationToken))
            return ConflictResult<ReadOrderDto>.Create(new Error("There is already a pending order for this game.", nameof(CreateOrderDto.GameId)));

        var order = new Order
        {
            UserId = userId,
            UserEmail = _currentUser.Email,
            GameId = game.Id,
            GameName = game.Name,
            Price = game.Price,
            CreatedAt = DateTime.UtcNow
        };

        await _orderRepository.AddAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new OrderPlacedEvent(order.Id, order.UserId, order.UserEmail, order.GameId, order.GameName, order.Price),
            cancellationToken);

        return SuccessResult<ReadOrderDto>.Create(MapToReadDto(order));
    }

    public async Task<Result<ReadOrderDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);

        if (order is null)
            return NotFoundResult<ReadOrderDto>.Create(new Error("Order not found."));

        if (order.UserId != _currentUser.UserId && !_currentUser.IsAdministrator)
            return ForbiddenResult<ReadOrderDto>.Create(new Error("You can only access your own orders."));

        return SuccessResult<ReadOrderDto>.Create(MapToReadDto(order));
    }

    public async Task<Result<Pagination<ReadOrderDto>>> GetPagedAsync(OrderFilter filter, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAdministrator)
        {
            if (filter.UserId.HasValue && filter.UserId != _currentUser.UserId)
                return ForbiddenResult<Pagination<ReadOrderDto>>.Create(new Error("You can only access your own orders."));

            filter.UserId = _currentUser.UserId;
        }

        var (items, totalCount) = await _orderRepository.GetPagedAsync(filter, cancellationToken);
        var mapped = items.Select(MapToReadDto).ToList();

        return SuccessResult<Pagination<ReadOrderDto>>.Create(
            new Pagination<ReadOrderDto>(mapped, totalCount, filter.CurrentPage, filter.PageSize));
    }

    public async Task<bool> ApplyPaymentResultAsync(PaymentProcessedEvent payment, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(payment.OrderId, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Payment result ignored: order {OrderId} not found.", payment.OrderId);
            return false;
        }

        var approved = payment.Status == PaymentStatus.Approved;

        if (!order.ApplyPaymentResult(approved, DateTime.UtcNow))
        {
            _logger.LogInformation("Payment result ignored: order {OrderId} was already {Status}.", order.Id, order.Status);
            return false;
        }

        await _orderRepository.UpdateAsync(order, cancellationToken);

        if (approved && !await _userGameRepository.ExistsAsync(order.UserId, order.GameId, cancellationToken))
        {
            await _userGameRepository.AddAsync(new UserGame
            {
                UserId = order.UserId,
                UserEmail = order.UserEmail,
                GameId = order.GameId,
                GameName = order.GameName,
                PricePaid = order.Price,
                OrderId = order.Id,
                PurchasedAt = order.ProcessedAt!.Value
            }, cancellationToken);
        }

        // Repositories share the scoped DbContext: a single SaveChanges commits the order status and the library entry atomically.
        await _orderRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} marked as {Status}.", order.Id, order.Status);
        return true;
    }

    private static ReadOrderDto MapToReadDto(Order order)
    {
        return new ReadOrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            UserEmail = order.UserEmail,
            GameId = order.GameId,
            GameName = order.GameName,
            Price = order.Price,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            ProcessedAt = order.ProcessedAt
        };
    }
}
