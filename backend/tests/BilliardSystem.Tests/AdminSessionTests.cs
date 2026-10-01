using BilliardSystem.Domain.Entities;
using FluentAssertions;

namespace BilliardSystem.Tests;

/// <summary>
/// Opaque refresh-token sessions: 30-day sliding expiry, revoked on rotation or
/// password reset.
/// </summary>
public sealed class AdminSessionTests
{
    private static AdminSession Create(DateTimeOffset? expiresAt = null) =>
        new("token-hash", expiresAt ?? DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void NewSession_IsValid()
    {
        Create().IsValid().Should().BeTrue();
    }

    [Fact]
    public void RevokedSession_IsInvalid()
    {
        var session = Create();

        session.Revoke();

        session.IsRevoked.Should().BeTrue();
        session.IsValid().Should().BeFalse();
    }

    [Fact]
    public void ExpiredSession_IsInvalid()
    {
        var session = Create(DateTimeOffset.UtcNow.AddSeconds(-1));

        session.IsValid().Should().BeFalse();
    }

    [Fact]
    public void Touch_ExtendsExpiryToThirtyDaysFromLastUse()
    {
        var session = Create(DateTimeOffset.UtcNow.AddDays(1));
        var before = session.ExpiresAt;

        session.Touch();

        session.ExpiresAt.Should().BeAfter(before);
        session.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Touch_NeverShrinksA_LongerExpiry()
    {
        var farFuture = DateTimeOffset.UtcNow.AddDays(90);
        var session = Create(farFuture);

        session.Touch();

        session.ExpiresAt.Should().Be(farFuture);
    }
}
