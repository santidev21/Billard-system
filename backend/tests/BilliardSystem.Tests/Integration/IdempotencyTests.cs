using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace BilliardSystem.Tests.Integration;

/// <summary>
/// Offline clients retry writes with the same transaction id. Every mutating
/// endpoint must apply the operation at most once.
/// </summary>
[Collection("api")]
public sealed class IdempotencyTests
{
    private readonly BilliardApiFactory _factory;

    public IdempotencyTests(BilliardApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RepeatedTransactionIds_ApplyEachOperationOnlyOnce()
    {
        var client = _factory.CreateClient();

        var tables = await client.GetFromJsonAsync<JsonElement>("/api/t/demo/tables");
        var tableId = tables.EnumerateArray().First().GetProperty("id").GetString()!;
        var baseUrl = $"/api/t/demo/tables/{tableId}";

        // start
        var startTx = Guid.NewGuid();
        var startPayload = new
        {
            whitePlayerName = "Ana",
            yellowPlayerName = "Beto",
            gameMode = "Managed",
            transactionId = startTx,
        };
        var firstStart = await client.PostAsJsonAsync($"{baseUrl}/start", startPayload);
        firstStart.StatusCode.Should().Be(HttpStatusCode.OK);
        var matchId = (await firstStart.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("matchId").GetString()!;

        (await client.PostAsJsonAsync($"{baseUrl}/start", startPayload)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detailAfterStart = await client.GetFromJsonAsync<JsonElement>(baseUrl);
        detailAfterStart.GetProperty("activeMatchId").GetString().Should().Be(matchId);

        // a different transaction cannot start a second match on an occupied table
        var conflictingStart = await client.PostAsJsonAsync($"{baseUrl}/start", new
        {
            whitePlayerName = "Ana",
            yellowPlayerName = "Beto",
            gameMode = "Managed",
            transactionId = Guid.NewGuid(),
        });
        conflictingStart.IsSuccessStatusCode.Should().BeFalse();

        // consumption applies once
        var products = await client.GetFromJsonAsync<JsonElement>("/api/t/demo/products");
        var product = products.EnumerateArray().First();
        var consumptionTx = Guid.NewGuid();
        var consumptionPayload = new { productId = product.GetProperty("id").GetString(), quantity = 2, transactionId = consumptionTx };

        (await client.PostAsJsonAsync($"{baseUrl}/consumption", consumptionPayload)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"{baseUrl}/consumption", consumptionPayload)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detailAfterConsumption = await client.GetFromJsonAsync<JsonElement>(baseUrl);
        detailAfterConsumption.GetProperty("activeMatch").GetProperty("consumptions").GetArrayLength().Should().Be(1);

        // score applies once
        var scoreTx = Guid.NewGuid();
        var scorePayload = new { playerColor = "white", delta = 3, transactionId = scoreTx };
        (await client.PostAsJsonAsync($"{baseUrl}/score", scorePayload)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"{baseUrl}/score", scorePayload)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detailAfterScore = await client.GetFromJsonAsync<JsonElement>(baseUrl);
        detailAfterScore.GetProperty("activeMatch").GetProperty("whiteScore").GetInt32().Should().Be(3);

        // finish applies once and frees the table
        var finishPayload = new { transactionId = Guid.NewGuid() };
        (await client.PostAsJsonAsync($"{baseUrl}/finish", finishPayload)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"{baseUrl}/finish", finishPayload)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detailAfterFinish = await client.GetFromJsonAsync<JsonElement>(baseUrl);
        detailAfterFinish.GetProperty("activeMatchId").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
