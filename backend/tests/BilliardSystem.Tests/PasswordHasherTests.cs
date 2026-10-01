using System.Security.Cryptography;
using System.Text;
using BilliardSystem.Domain.Common;
using FluentAssertions;

namespace BilliardSystem.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesVersionedPbkdf2Hash()
    {
        var hash = PasswordHasher.Hash("SuperSecret1");

        hash.Should().StartWith("v2.");
        PasswordHasher.IsLegacyHash(hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_IsSalted_SoTheSamePasswordYieldsDifferentHashes()
    {
        PasswordHasher.Hash("SuperSecret1").Should().NotBe(PasswordHasher.Hash("SuperSecret1"));
    }

    [Fact]
    public void Verify_AcceptsTheCorrectPassword_AndRejectsOthers()
    {
        var hash = PasswordHasher.Hash("SuperSecret1");

        PasswordHasher.Verify("SuperSecret1", hash).Should().BeTrue();
        PasswordHasher.Verify("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsMalformedVersionedHash()
    {
        PasswordHasher.Verify("x", "v2.not-a-valid-hash").Should().BeFalse();
    }

    [Fact]
    public void Verify_StillAcceptsLegacyUnsaltedSha256Hash()
    {
        var legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("legacy-pass")));

        PasswordHasher.IsLegacyHash(legacy).Should().BeTrue();
        PasswordHasher.Verify("legacy-pass", legacy).Should().BeTrue();
        PasswordHasher.Verify("nope", legacy).Should().BeFalse();
    }
}
