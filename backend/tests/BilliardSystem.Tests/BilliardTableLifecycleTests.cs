using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using BilliardSystem.Domain.Events;
using FluentAssertions;

namespace BilliardSystem.Tests;

/// <summary>
/// Table lifecycle beyond the happy path: code normalization, out-of-service
/// transitions and waiter/check signalling.
/// </summary>
public sealed class BilliardTableLifecycleTests
{
    private static BilliardTable CreateTable() => new("Mesa 1", 12000m, Guid.NewGuid());

    [Fact]
    public void SetCode_TrimsAndUppercases()
    {
        var table = CreateTable();

        table.SetCode("  m1  ");

        table.Code.Should().Be("M1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetCode_WithBlankValue_Throws(string code)
    {
        var table = CreateTable();

        var act = () => table.SetCode(code);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Disable_WithActiveMatch_Throws()
    {
        var table = CreateTable();
        table.StartSession(Guid.NewGuid(), "Blanco", "Amarillo", null);

        var act = () => table.Disable();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Disable_WhenIdle_BecomesOutOfService_AndEnableRestores()
    {
        var table = CreateTable();

        table.Disable();
        table.IsActive.Should().BeFalse();
        table.Status.Should().Be(BilliardTableStatus.OutOfService);

        table.Enable();
        table.IsActive.Should().BeTrue();
        table.Status.Should().Be(BilliardTableStatus.Available);
    }

    [Fact]
    public void MarkWaiterRequested_And_MarkCheckRequested_RaiseEvents()
    {
        var table = CreateTable();
        var matchId = Guid.NewGuid();
        table.StartSession(matchId, "Blanco", "Amarillo", null);
        table.ClearDomainEvents();

        table.MarkWaiterRequested(matchId);
        table.Status.Should().Be(BilliardTableStatus.WaitingForWaiter);
        table.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WaiterRequestedEvent>();

        table.ClearDomainEvents();
        table.MarkCheckRequested(matchId);
        table.Status.Should().Be(BilliardTableStatus.WaitingForCheck);
        table.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CheckRequestedEvent>();
    }

    [Fact]
    public void MarkAttended_ReturnsToOccupied_OnlyFromWaitingStates()
    {
        var table = CreateTable();
        var matchId = Guid.NewGuid();
        table.StartSession(matchId, "Blanco", "Amarillo", null);

        table.MarkAttended();
        table.Status.Should().Be(BilliardTableStatus.Occupied, "an occupied table ignores attendance");

        table.MarkCheckRequested(matchId);
        table.MarkAttended();
        table.Status.Should().Be(BilliardTableStatus.Occupied);
    }

    [Fact]
    public void EndSession_WithWrongMatch_Throws()
    {
        var table = CreateTable();
        table.StartSession(Guid.NewGuid(), "Blanco", "Amarillo", null);

        var act = () => table.EndSession(Guid.NewGuid(), null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EndSession_ClearsActiveMatch_AndRaisesEvent()
    {
        var table = CreateTable();
        var matchId = Guid.NewGuid();
        table.StartSession(matchId, "Blanco", "Amarillo", null);
        table.ClearDomainEvents();

        table.EndSession(matchId, Guid.NewGuid());

        table.ActiveMatchId.Should().BeNull();
        table.Status.Should().Be(BilliardTableStatus.Available);
        table.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<SessionEndedEvent>();
    }
}
