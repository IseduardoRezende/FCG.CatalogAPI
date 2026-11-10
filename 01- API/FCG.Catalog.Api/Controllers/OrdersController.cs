using FCG.Catalog.Api.Extensions;
using FCG.Catalog.Application.DTOs.Orders;
using FCG.Catalog.Application.Services.Interfaces;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Filters;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FCG.Catalog.Api.Controllers;

[Route("api/v{version:apiVersion}/orders")]
public class OrdersController : BaseController
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Place an order for a game (publishes OrderPlacedEvent; payment is processed asynchronously)")]
    [ProducesResponseType(typeof(SuccessResult<ReadOrderDto>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> PlaceAsync([FromBody] CreateOrderDto dto, CancellationToken cancellationToken)
    {
        return (await _orderService.PlaceAsync(dto, cancellationToken)).ToActionResult(StatusCodes.Status202Accepted);
    }

    [HttpGet]
    [SwaggerOperation(Summary = "List orders (own orders; administrators can see all)")]
    [ProducesResponseType(typeof(SuccessResult<Pagination<ReadOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPagedAsync([FromQuery] OrderFilter filter, CancellationToken cancellationToken)
    {
        return (await _orderService.GetPagedAsync(filter, cancellationToken)).ToActionResult();
    }

    [HttpGet("{id:long}")]
    [SwaggerOperation(Summary = "Get order by id (check the payment status)")]
    [ProducesResponseType(typeof(SuccessResult<ReadOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        return (await _orderService.GetByIdAsync(id, cancellationToken)).ToActionResult();
    }
}
