using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace FCG.Catalog.IntegrationTests.Infrastructure;

public static class TestClient
{
    // Must match TokenSettings in appsettings.json: tokens are issued by the Users API in production.
    private const string Issuer = "FCG.Users";
    private const string Audience = "FCG.Client";
    private const string Key = "local-dev-only-key-change-me-0123456789-abcdefghijklmnopqrstuvwxyz";

    private static long _nextUserId = Random.Shared.Next(1_000, 100_000);

    public static long NewUserId() => Interlocked.Increment(ref _nextUserId);

    public static HttpClient AsUser(this CatalogApiFactory factory, long userId, string role = "User")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId, role));
        return client;
    }

    public static HttpClient AsAdmin(this CatalogApiFactory factory) => factory.AsUser(-1, "Administrator");

    public static string EmailOf(long userId) => $"player{userId}@test.com";

    public static async Task<JsonElement> ReadValueAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("value").Clone();
    }

    public static async Task<long> CreateGameAsync(this CatalogApiFactory factory, decimal price, CancellationToken cancellationToken)
    {
        var response = await factory.AsAdmin().PostAsJsonAsync("/api/v1/games",
            new { name = $"Game {Guid.NewGuid():N}"[..20], description = "Integration test game", price }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.ReadValueAsync(cancellationToken)).GetProperty("id").GetInt64();
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken cancellationToken)
    {
        var timeout = DateTime.UtcNow.AddSeconds(15);

        while (!await condition())
        {
            if (DateTime.UtcNow > timeout)
                throw new TimeoutException("Condition was not met within 15 seconds.");

            await Task.Delay(200, cancellationToken);
        }
    }

    private static string CreateToken(long userId, string role)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, EmailOf(userId)),
            new Claim(ClaimTypes.Role, role)
        };

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(Issuer, Audience, claims, expires: DateTime.UtcNow.AddHours(1), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
