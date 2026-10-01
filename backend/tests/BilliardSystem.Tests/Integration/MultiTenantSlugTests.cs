using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace BilliardSystem.Tests.Integration;

/// <summary>
/// Multi-tenant routing by slug: each hall's catalogue is addressed through
/// <c>/t/{slug}/...</c> and unknown slugs are rejected.
/// </summary>
[Collection("api")]
public sealed class MultiTenantSlugTests
{
    private readonly BilliardApiFactory _factory;

    public MultiTenantSlugTests(BilliardApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DemoHall_ExposesTablesAndProductsBySlug()
    {
        var client = _factory.CreateClient();

        var tables = await client.GetFromJsonAsync<JsonElement>("/api/t/demo/tables");
        tables.GetArrayLength().Should().BePositive();
        tables.EnumerateArray().Select(t => t.GetProperty("code").GetString()).Should().Contain("M1");

        var products = await client.GetFromJsonAsync<JsonElement>("/api/t/demo/products");
        products.GetArrayLength().Should().BePositive();
        products.EnumerateArray().Should().OnlyContain(p => p.GetProperty("price").GetDecimal() > 0m);
    }

    [Fact]
    public async Task UnknownSlug_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/t/no-such-hall/tables");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Table_CanBeResolvedByCodeWithinTheTenant()
    {
        var client = _factory.CreateClient();

        var detail = await client.GetFromJsonAsync<JsonElement>("/api/t/demo/tables/M1");

        detail.GetProperty("code").GetString().Should().Be("M1");
    }
}
