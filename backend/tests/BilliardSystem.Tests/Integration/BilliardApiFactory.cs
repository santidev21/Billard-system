using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace BilliardSystem.Tests.Integration;

/// <summary>
/// Boots the real API pipeline (<c>WebApplicationFactory</c>) against a throwaway
/// PostgreSQL container, so endpoints, EF Core (Postgres-only features such as
/// <c>ExecuteDelete</c>) and migrations are all exercised for real.
/// </summary>
public sealed class BilliardApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SuperUserName = "superadmin";
    private const string SuperPassword = "SuperAdmin123!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private string? _superAdminToken;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    public async Task<string> GetSuperAdminTokenAsync()
    {
        if (_superAdminToken is not null)
        {
            return _superAdminToken;
        }

        var response = await CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { userName = SuperUserName, password = SuperPassword });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        _superAdminToken = body.GetProperty("accessToken").GetString()!;
        return _superAdminToken;
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        // Read by WebApplication.CreateBuilder when the app is first booted, so the
        // API migrates and seeds the throwaway container instead of the local DB.
        Environment.SetEnvironmentVariable("ConnectionStrings__BilliardDatabase", _postgres.GetConnectionString());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<BilliardApiFactory>;
