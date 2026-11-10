using System.Net;
using System.Net.Http.Json;
using FCG.Catalog.IntegrationTests.Infrastructure;

namespace FCG.Catalog.IntegrationTests;

public class GamesEndpointsTests
{
    private readonly CatalogApiFactory _factory;

    public GamesEndpointsTests(CatalogApiFactory factory)
    {
        _factory = factory;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object ValidGame => new { name = "Celeste", description = "Platformer", price = 39.90m };

    [Fact]
    public async Task CreateGame_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/games", ValidGame, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateGame_WithCommonUserToken_Returns403()
    {
        var response = await _factory.AsUser(TestClient.NewUserId()).PostAsJsonAsync("/api/v1/games", ValidGame, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateGame_WithInvalidPrice_Returns422()
    {
        var response = await _factory.AsAdmin().PostAsJsonAsync("/api/v1/games", new { name = "Free?", description = "No", price = 0 }, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GameCrud_WithAdministratorToken_CreatesReadsUpdatesAndSoftDeletes()
    {
        var admin = _factory.AsAdmin();

        var created = await admin.PostAsJsonAsync("/api/v1/games", ValidGame, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.ReadValueAsync(Ct)).GetProperty("id").GetInt64();

        var updated = await admin.PutAsJsonAsync($"/api/v1/games/{id}", new { name = "Celeste DX", description = "Remaster", price = 49.90m }, Ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var read = await (await admin.GetAsync($"/api/v1/games/{id}", Ct)).ReadValueAsync(Ct);
        Assert.Equal("Celeste DX", read.GetProperty("name").GetString());
        Assert.Equal(49.90m, read.GetProperty("price").GetDecimal());

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/games/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/games/{id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task ListGames_WithCommonUserToken_Returns200()
    {
        await _factory.CreateGameAsync(10m, Ct);

        var response = await _factory.AsUser(TestClient.NewUserId()).GetAsync("/api/v1/games", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadValueAsync(Ct)).GetProperty("totalCount").GetInt32() > 0);
    }

    [Fact]
    public async Task Health_WhenDatabaseIsReachable_Returns200()
    {
        var response = await _factory.CreateClient().GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
