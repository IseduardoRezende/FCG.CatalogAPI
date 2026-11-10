using FCG.Catalog.Api.Extensions;
using FCG.Catalog.Application.DTOs.UserGames;
using FCG.Catalog.Application.Services.Interfaces;
using FCG.Catalog.Domain.Commons;
using FCG.Catalog.Domain.Commons.Result;
using FCG.Catalog.Domain.Filters;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FCG.Catalog.Api.Controllers;

[Route("api/v{version:apiVersion}/user-games")]
public class UserGamesController : BaseController
{
    private readonly IUserGameService _userGameService;

    public UserGamesController(IUserGameService userGameService)
    {
        _userGameService = userGameService;
    }

    [HttpGet]
    [SwaggerOperation(Summary = "List user game library (own library; administrators can filter by user)")]
    [ProducesResponseType(typeof(SuccessResult<Pagination<ReadUserGameDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLibraryAsync([FromQuery] UserGameFilter filter, CancellationToken cancellationToken)
    {
        return (await _userGameService.GetLibraryAsync(filter, cancellationToken)).ToActionResult();
    }

    [HttpGet("{id:long}")]
    [SwaggerOperation(Summary = "Get purchased game by id")]
    [ProducesResponseType(typeof(SuccessResult<ReadUserGameDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        return (await _userGameService.GetByIdAsync(id, cancellationToken)).ToActionResult();
    }
}
