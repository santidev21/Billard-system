using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BilliardSystem.API.Auth;

public sealed class AdminAuthOptions : AuthenticationSchemeOptions
{
}

public sealed class AdminAuthHandler : AuthenticationHandler<AdminAuthOptions>
{
    private const int RefreshThresholdHours = 24;

    public AdminAuthHandler(
        IOptionsMonitor<AdminAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var raw = authHeader.ToString();
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = raw["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        var scope = Context.RequestServices.CreateScope();
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BilliardDbContext>();

            var session = await dbContext.Sessions
                .FirstOrDefaultAsync(s => s.TokenHash == tokenHash);

            if (session is null || !session.IsValid())
            {
                return AuthenticateResult.Fail("Sesión inválida o expirada.");
            }

            if (DateTimeOffset.UtcNow - session.LastUsedAt > TimeSpan.FromHours(RefreshThresholdHours))
            {
                session.Touch();
                await dbContext.SaveChangesAsync();
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Admin"),
                new Claim("session_id", session.Id.ToString())
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        finally
        {
            (scope as IDisposable)?.Dispose();
        }
    }
}
