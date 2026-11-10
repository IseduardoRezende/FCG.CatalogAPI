using System.Net;
using System.Net.Http.Json;
using FCG.Catalog.IntegrationTests.Infrastructure;
using FCG.Contracts.Events;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Catalog.IntegrationTests;

public class PurchaseFlowTests
{
    private readonly CatalogApiFactory _factory;

    public PurchaseFlowTests(CatalogApiFactory factory)
    {
        _factory = factory;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ITestHarness Harness => _factory.Services.GetTestHarness();

    private static async Task<HttpResponseMessage> PlaceOrderAsync(HttpClient client, long gameId) =>
        await client.PostAsJsonAsync("/api/v1/orders", new { gameId }, Ct);

    private async Task<long> PlaceOrderAndGetIdAsync(HttpClient client, long gameId)
    {
        var response = await PlaceOrderAsync(client, gameId);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.ReadValueAsync(Ct)).GetProperty("id").GetInt64();
    }

    private async Task PublishPaymentAsync(long orderId, long userId, long gameId, PaymentStatus status)
    {
        await Harness.Bus.Publish(new PaymentProcessedEvent(orderId, userId, TestClient.EmailOf(userId), gameId, "Game", 10m, status), Ct);
    }

    private static async Task<string> GetOrderStatusAsync(HttpClient client, long orderId) =>
        (await (await client.GetAsync($"/api/v1/orders/{orderId}", Ct)).ReadValueAsync(Ct)).GetProperty("status").GetString()!;

    private static async Task WaitForOrderStatusAsync(HttpClient client, long orderId, string expected) =>
        await TestClient.WaitUntilAsync(async () => await GetOrderStatusAsync(client, orderId) == expected, Ct);

    private static async Task<int> CountLibraryAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/v1/user-games", Ct)).ReadValueAsync(Ct)).GetProperty("totalCount").GetInt32();

    [Fact]
    public async Task PlaceOrder_ForAvailableGame_Returns202WithPendingOrder()
    {
        var gameId = await _factory.CreateGameAsync(59.90m, Ct);
        var userId = TestClient.NewUserId();

        var response = await PlaceOrderAsync(_factory.AsUser(userId), gameId);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var order = await response.ReadValueAsync(Ct);
        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.Equal(59.90m, order.GetProperty("price").GetDecimal());
        Assert.Equal(TestClient.EmailOf(userId), order.GetProperty("userEmail").GetString());
    }

    [Fact]
    public async Task PlaceOrder_ForAvailableGame_PublishesOrderPlacedEventWithPriceAndEmail()
    {
        var gameId = await _factory.CreateGameAsync(59.90m, Ct);
        var userId = TestClient.NewUserId();

        var orderId = await PlaceOrderAndGetIdAsync(_factory.AsUser(userId), gameId);

        Assert.True(await Harness.Published.Any<OrderPlacedEvent>(m =>
            m.Context.Message.OrderId == orderId &&
            m.Context.Message.Price == 59.90m &&
            m.Context.Message.UserEmail == TestClient.EmailOf(userId), Ct));
    }

    [Fact]
    public async Task PlaceOrder_WhileAnotherOrderForSameGameIsPending_Returns409()
    {
        var gameId = await _factory.CreateGameAsync(10m, Ct);
        var client = _factory.AsUser(TestClient.NewUserId());
        await PlaceOrderAndGetIdAsync(client, gameId);

        var response = await PlaceOrderAsync(client, gameId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PlaceOrder_ForNonexistentGame_Returns404()
    {
        var response = await PlaceOrderAsync(_factory.AsUser(TestClient.NewUserId()), 999_999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PlaceOrder_WithoutToken_Returns401()
    {
        var response = await PlaceOrderAsync(_factory.CreateClient(), 1);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApprovedPayment_ForPendingOrder_ApprovesOrderAndAddsGameToLibrary()
    {
        var gameId = await _factory.CreateGameAsync(10m, Ct);
        var userId = TestClient.NewUserId();
        var client = _factory.AsUser(userId);
        var orderId = await PlaceOrderAndGetIdAsync(client, gameId);

        await PublishPaymentAsync(orderId, userId, gameId, PaymentStatus.Approved);

        await WaitForOrderStatusAsync(client, orderId, "Approved");
        var library = await (await client.GetAsync("/api/v1/user-games", Ct)).ReadValueAsync(Ct);
        var item = Assert.Single(library.GetProperty("items").EnumerateArray());
        Assert.Equal(gameId, item.GetProperty("gameId").GetInt64());
        Assert.Equal(orderId, item.GetProperty("orderId").GetInt64());
    }

    [Fact]
    public async Task ApprovedPayment_ThenOrderingSameGameAgain_Returns409BecauseGameIsOwned()
    {
        var gameId = await _factory.CreateGameAsync(10m, Ct);
        var userId = TestClient.NewUserId();
        var client = _factory.AsUser(userId);
        var orderId = await PlaceOrderAndGetIdAsync(client, gameId);
        await PublishPaymentAsync(orderId, userId, gameId, PaymentStatus.Approved);
        await WaitForOrderStatusAsync(client, orderId, "Approved");

        var response = await PlaceOrderAsync(client, gameId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RejectedPayment_ForPendingOrder_RejectsOrderAndKeepsLibraryEmpty()
    {
        var gameId = await _factory.CreateGameAsync(10m, Ct);
        var userId = TestClient.NewUserId();
        var client = _factory.AsUser(userId);
        var orderId = await PlaceOrderAndGetIdAsync(client, gameId);

        await PublishPaymentAsync(orderId, userId, gameId, PaymentStatus.Rejected);

        await WaitForOrderStatusAsync(client, orderId, "Rejected");
        Assert.Equal(0, await CountLibraryAsync(client));
    }

    [Fact]
    public async Task RejectedPayment_ThenOrderingSameGameAgain_AllowsNewOrder()
    {
        var gameId = await _factory.CreateGameAsync(10m, Ct);
        var userId = TestClient.NewUserId();
        var client = _factory.AsUser(userId);
        var orderId = await PlaceOrderAndGetIdAsync(client, gameId);
        await PublishPaymentAsync(orderId, userId, gameId, PaymentStatus.Rejected);
        await WaitForOrderStatusAsync(client, orderId, "Rejected");

        var response = await PlaceOrderAsync(client, gameId);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task DuplicatedApprovedPayment_ForSameOrder_AddsGameToLibraryOnlyOnce()
    {
        var gameId = await _factory.CreateGameAsync(10m, Ct);
        var userId = TestClient.NewUserId();
        var client = _factory.AsUser(userId);
        var orderId = await PlaceOrderAndGetIdAsync(client, gameId);

        await PublishPaymentAsync(orderId, userId, gameId, PaymentStatus.Approved);
        await PublishPaymentAsync(orderId, userId, gameId, PaymentStatus.Approved);

        await WaitForOrderStatusAsync(client, orderId, "Approved");
        await TestClient.WaitUntilAsync(() => Task.FromResult(
            Harness.Consumed.Select<PaymentProcessedEvent>(m => m.Context.Message.OrderId == orderId).Count() == 2), Ct);
        Assert.Equal(1, await CountLibraryAsync(client));
    }

    [Fact]
    public async Task GetLibrary_ForAnotherUserAsCommonUser_Returns403()
    {
        var response = await _factory.AsUser(TestClient.NewUserId()).GetAsync("/api/v1/user-games?userId=1", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_OfAnotherUser_Returns403()
    {
        var gameId = await _factory.CreateGameAsync(10m, Ct);
        var orderId = await PlaceOrderAndGetIdAsync(_factory.AsUser(TestClient.NewUserId()), gameId);

        var response = await _factory.AsUser(TestClient.NewUserId()).GetAsync($"/api/v1/orders/{orderId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
