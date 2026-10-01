using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace BilliardSystem.Tests.Integration;

/// <summary>
/// End-to-end password recovery: an admin loses the password, the super admin
/// reveals a code and the admin resets with that exact code. This is the flow
/// that was broken when the revealed code hash was not persisted.
/// </summary>
[Collection("api")]
public sealed class PasswordRecoveryFlowTests
{
    private readonly BilliardApiFactory _factory;

    public PasswordRecoveryFlowTests(BilliardApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RevealedCode_ResetsThePassword_EndToEnd()
    {
        var anonymous = _factory.CreateClient();
        var superAdmin = _factory.CreateClient();
        superAdmin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await _factory.GetSuperAdminTokenAsync());

        // Super admin creates a hall; its administrator user name is the slug.
        var localName = "Local " + Guid.NewGuid().ToString("N")[..8];
        var createLocal = await superAdmin.PostAsJsonAsync(
            "/api/super/locals", new { name = localName, initialPassword = "admin123" });
        createLocal.StatusCode.Should().Be(HttpStatusCode.OK);
        var slug = (await createLocal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;

        // The admin requests a recovery; the super admin lists and reveals it.
        (await anonymous.PostAsJsonAsync("/api/auth/forgot", new { userName = slug }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var recoveries = await superAdmin.GetFromJsonAsync<JsonElement>("/api/super/recoveries");
        var request = recoveries.EnumerateArray()
            .First(r => r.GetProperty("userName").GetString() == slug);
        var requestId = request.GetProperty("id").GetString()!;

        // Reveal must persist the disclosed code, otherwise /auth/reset can never match.
        var reveal = await superAdmin.PostAsync($"/api/super/recoveries/{requestId}/reveal", null);
        reveal.StatusCode.Should().Be(HttpStatusCode.OK);
        var code = (await reveal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
        code.Should().MatchRegex("^\\d{8}$");

        var reset = await anonymous.PostAsJsonAsync(
            "/api/auth/reset", new { userName = slug, code, newPassword = "NewSecret123!" });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        var relogin = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { userName = slug, password = "NewSecret123!" });
        relogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
