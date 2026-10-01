using BilliardSystem.Domain.Entities;
using FluentAssertions;

namespace BilliardSystem.Tests;

/// <summary>
/// Multi-tenant slug generation: lowercase, accent-stripped, dash-separated and
/// stable enough to be used in <c>/t/{slug}/...</c> URLs.
/// </summary>
public sealed class TenantTests
{
    [Theory]
    [InlineData("Billar Tres Bandas", "billar-tres-bandas")]
    [InlineData("Café Ñandú", "cafe-nandu")]
    [InlineData("Billar_Test", "billar-test")]
    [InlineData("  Hola  ", "hola")]
    [InlineData("A   B", "a-b")]
    [InlineData("Bar #1 @ Centro!", "bar-1-centro")]
    [InlineData("!!!", "local")]
    public void Slug_IsNormalized(string name, string expected)
    {
        new Tenant(name).Slug.Should().Be(expected);
    }

    [Fact]
    public void NewTenant_IsActive()
    {
        new Tenant("Demo").IsActive.Should().BeTrue();
    }

    [Fact]
    public void Rename_RegeneratesSlug()
    {
        var tenant = new Tenant("Uno");

        tenant.Rename("Dos Local");

        tenant.Name.Should().Be("Dos Local");
        tenant.Slug.Should().Be("dos-local");
    }

    [Fact]
    public void Deactivate_And_Activate_FlipTheFlag()
    {
        var tenant = new Tenant("Demo");

        tenant.Deactivate();
        tenant.IsActive.Should().BeFalse();

        tenant.Activate();
        tenant.IsActive.Should().BeTrue();
    }
}
