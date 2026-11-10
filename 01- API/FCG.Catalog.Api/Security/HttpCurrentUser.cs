using System.Security.Claims;
using FCG.Catalog.Application.Abstractions;
using FCG.Catalog.Domain.Enums;

namespace FCG.Catalog.Api.Security;

public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public long? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");
            return long.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.FindFirstValue("email");

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsAdministrator => Role == nameof(UserRoles.Administrator);
}
