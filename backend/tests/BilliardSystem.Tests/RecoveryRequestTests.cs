using BilliardSystem.Domain.Entities;
using FluentAssertions;

namespace BilliardSystem.Tests;

/// <summary>
/// Covers the password-recovery reveal -> reset invariant: the code disclosed by
/// the super-admin (reveal) must be the one <c>/auth/reset</c> matches, and the
/// request is only resolved on a successful reset.
/// </summary>
public sealed class RecoveryRequestTests
{
    private static RecoveryRequest Create(DateTimeOffset? expiresAt = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "hash-from-creation", expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(30));

    [Fact]
    public void NewRequest_StartsWithCreationHash_AndIsPending()
    {
        var request = Create();

        request.CodeHash.Should().Be("hash-from-creation");
        request.IsResolved.Should().BeFalse();
        request.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void ReplaceCode_SwapsStoredHash_WithoutResolving()
    {
        var request = Create();

        request.ReplaceCode("hash-of-revealed-code");

        request.CodeHash.Should().Be("hash-of-revealed-code");
        request.IsResolved.Should().BeFalse("resolution happens on a successful reset, not on reveal");
    }

    [Fact]
    public void Resolve_MarksRequestResolved()
    {
        var request = Create();

        request.Resolve();

        request.IsResolved.Should().BeTrue();
        request.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void IsExpired_IsFalseBeforeDeadline_AndTrueAfter()
    {
        Create(DateTimeOffset.UtcNow.AddMinutes(1)).IsExpired().Should().BeFalse();
        Create(DateTimeOffset.UtcNow.AddMinutes(-1)).IsExpired().Should().BeTrue();
    }
}
