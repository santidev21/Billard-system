using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using FluentAssertions;

namespace BilliardSystem.Tests;

/// <summary>
/// Scoring and consumption rules of a match beyond the happy path: colour
/// handling, quantity bounds and free-mode billing.
/// </summary>
public sealed class MatchHistoryBehaviorTests
{
    private static MatchHistory CreateGame(GameMode mode = GameMode.Managed) =>
        new(Guid.NewGuid(), "Blanco", "Amarillo", 12000m, openedByUserId: null, mode, Guid.NewGuid());

    [Theory]
    [InlineData("white")]
    [InlineData("WHITE")]
    [InlineData("Yellow")]
    [InlineData("YELLOW")]
    public void AddScore_AcceptsPlayerColorCaseInsensitively(string color)
    {
        var match = CreateGame();

        var log = match.AddScore(color, 2, userId: null);

        log.PlayerColor.Should().Be(color.Equals("yellow", StringComparison.OrdinalIgnoreCase) ? "Yellow" : "White");
        match.TotalCarambolas.Should().Be(2);
    }

    [Fact]
    public void RenamePlayer_UpdatesOnlyTheRequestedSide()
    {
        var match = CreateGame();

        match.RenamePlayer("yellow", "Nuevo Amarillo");
        match.RenamePlayer("white", "Nuevo Blanco");

        match.YellowPlayerName.Should().Be("Nuevo Amarillo");
        match.WhitePlayerName.Should().Be("Nuevo Blanco");
    }

    [Fact]
    public void UpdateConsumption_RecalculatesTotal()
    {
        var match = CreateGame();
        var consumption = match.AddConsumption(Guid.NewGuid(), "Cerveza", 5000m, 1);

        match.UpdateConsumption(consumption.Id, 3);

        consumption.Quantity.Should().Be(3);
        match.ConsumptionTotal.Should().Be(15000m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void UpdateConsumption_OutsideAllowedRange_Throws(int quantity)
    {
        var match = CreateGame();
        var consumption = match.AddConsumption(Guid.NewGuid(), "Cerveza", 5000m, 1);

        var act = () => match.UpdateConsumption(consumption.Id, quantity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateConsumption_UnknownId_Throws()
    {
        var match = CreateGame();

        var act = () => match.UpdateConsumption(Guid.NewGuid(), 2);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemoveConsumption_RemovesAndRecalculatesTotal()
    {
        var match = CreateGame();
        var first = match.AddConsumption(Guid.NewGuid(), "Agua", 3000m, 2);
        match.AddConsumption(Guid.NewGuid(), "Cerveza", 5000m, 1);

        match.RemoveConsumption(first.Id);

        match.Consumptions.Should().HaveCount(1);
        match.ConsumptionTotal.Should().Be(5000m);
    }

    [Fact]
    public void Close_InFreeMode_ZeroesTableAndConsumptionTotals()
    {
        var match = CreateGame(GameMode.FreeMode);

        match.Close(match.StartedAt.AddMinutes(30), tableTotal: 6000m, consumptionTotal: 5000m, closedByUserId: null);

        match.TableTotal.Should().Be(0m);
        match.ConsumptionTotal.Should().Be(0m);
        match.GrandTotal.Should().Be(0m);
    }

    [Fact]
    public void CloseRound_SetsWinnerAndResetsScoresAndLogs()
    {
        var match = CreateGame();
        match.AddScore("white", 5, null);
        match.AddScore("yellow", 2, null);

        var round = match.CloseRound(match.StartedAt.AddSeconds(40));

        round.WinnerName.Should().Be("Blanco");
        round.WhiteScore.Should().Be(5);
        round.YellowScore.Should().Be(2);
        match.WhiteScore.Should().Be(0);
        match.YellowScore.Should().Be(0);
        match.ScoreLogs.Should().BeEmpty();
    }

    [Fact]
    public void CloseRound_WithTiedScore_HasNoWinner()
    {
        var match = CreateGame();
        match.AddScore("white", 3, null);
        match.AddScore("yellow", 3, null);

        var round = match.CloseRound(match.StartedAt.AddSeconds(10));

        round.WinnerName.Should().BeNull();
    }
}
