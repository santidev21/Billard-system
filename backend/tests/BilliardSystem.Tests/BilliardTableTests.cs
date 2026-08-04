using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using BilliardSystem.Domain.Events;
using FluentAssertions;

namespace BilliardSystem.Tests;

public sealed class BilliardTableTests
{
    [Fact]
    public void StartSession_WhenTableIsAvailable_MarksTableOccupiedAndRaisesEvent()
    {
        var table = new BilliardTable("Mesa 1", 12000m);
        var matchId = Guid.NewGuid();

        table.StartSession(matchId, "Blanco", "Amarillo", employeeId: null);

        table.Status.Should().Be(BilliardTableStatus.Occupied);
        table.ActiveMatchId.Should().Be(matchId);
        table.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SessionStartedEvent>();
    }

    [Fact]
    public void StartSession_WhenTableIsAlreadyOccupied_Throws()
    {
        var table = new BilliardTable("Mesa 1", 12000m);
        table.StartSession(Guid.NewGuid(), "Blanco", "Amarillo", employeeId: null);

        var act = () => table.StartSession(Guid.NewGuid(), "Blanco", "Amarillo", employeeId: null);

        act.Should().Throw<InvalidOperationException>();
    }
}
