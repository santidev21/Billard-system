using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using FluentAssertions;

namespace BilliardSystem.Tests;

public sealed class MatchHistoryTests
{
    private static MatchHistory CreateGame() =>
        new(
            Guid.NewGuid(),
            "Blanco",
            "Amarillo",
            12000m,
            openedByUserId: null,
            gameMode: GameMode.Managed,
            tenantId: Guid.NewGuid());

    [Fact]
    public void AddScore_WhenPositiveIncrements_UpdatesScoreAndTotal()
    {
        var match = CreateGame();

        var result = match.AddScore("white", 3, userId: null);

        match.AddScore("yellow", 2, userId: null);

        result.ResultingScore.Should().Be(3);
        match.WhiteScore.Should().Be(3);
        match.YellowScore.Should().Be(2);
        match.TotalCarambolas.Should().Be(5);
        match.ScoreLogs.Should().HaveCount(2);
    }

    [Fact]
    public void AddScore_NeverGoesBelowZero()
    {
        var match = CreateGame();

        match.AddScore("white", 2, userId: null);
        var result = match.AddScore("white", -5, userId: null);

        result.ResultingScore.Should().Be(0);
        match.WhiteScore.Should().Be(0);
    }

    [Fact]
    public void AddConsumption_IncrementsConsumptionTotal()
    {
        var match = CreateGame();

        match.AddConsumption(Guid.NewGuid(), "Agua", 3000m, 2);
        match.AddConsumption(Guid.NewGuid(), "Cerveza", 5000m, 1);

        match.ConsumptionTotal.Should().Be(11000m);
        match.Consumptions.Should().HaveCount(2);
    }

    [Fact]
    public void Close_ComputesGrandTotalAndEndedAt()
    {
        var match = CreateGame();

        match.Close(match.StartedAt.AddMinutes(30), tableTotal: 6000m, consumptionTotal: 5000m, closedByUserId: Guid.NewGuid());

        match.GrandTotal.Should().Be(11000m);
        match.TableTotal.Should().Be(6000m);
        match.ConsumptionTotal.Should().Be(5000m);
        match.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public void GameMode_DefaultsToManaged()
    {
        var match = CreateGame();
        match.GameMode.Should().Be(GameMode.Managed);
    }

    [Fact]
    public void Close_BillingMatchesTimeRule()
    {
        var match = CreateGame();

        var tableTotal = Math.Round((90m / 60m) * 12000m, 2);
        match.Close(match.StartedAt.AddMinutes(90), tableTotal, 0m, closedByUserId: null);

        match.TableTotal.Should().Be(18000m);
    }

    [Fact]
    public void CloseRound_ConsecutiveRoundsUsePreviousEndAsStart()
    {
        var match = CreateGame();
        var t0 = match.StartedAt;
        var t1 = t0.AddSeconds(30);
        var t2 = t0.AddSeconds(60);

        match.AddScore("white", 3, null);
        match.AddScore("yellow", 3, null);
        var r1 = match.CloseRound(t1);

        match.AddScore("white", 1, null);
        var r2 = match.CloseRound(t2);

        r1.StartedAt.Should().Be(t0);
        r1.EndedAt.Should().Be(t1);
        r1.DurationSeconds.Should().Be(30);
        r2.StartedAt.Should().Be(t1);
        r2.EndedAt.Should().Be(t2);
        r2.DurationSeconds.Should().Be(30);
        r2.Duration.Should().Be(t2 - t1);
    }

    [Fact]
    public void CloseRound_DurationSeconds_IsEndMinusStart()
    {
        var match = CreateGame();
        var t0 = match.StartedAt;
        var t1 = t0.AddSeconds(45);
        match.AddScore("white", 5, null);
        var r1 = match.CloseRound(t1);
        r1.DurationSeconds.Should().Be(45);
    }

    [Fact]
    public void TryCloseFinalRound_CreatesRoundWhenTimeElapsed()
    {
        var match = CreateGame();
        var t0 = match.StartedAt;
        var t1 = t0.AddMinutes(5);
        match.AddScore("white", 2, null);
        var final = match.TryCloseFinalRound(t1);
        final.Should().NotBeNull();
        final!.StartedAt.Should().Be(t0);
        final.EndedAt.Should().Be(t1);
        final.DurationSeconds.Should().Be(300);
        final.WhiteScore.Should().Be(2);
        match.Rounds.Should().HaveCount(1);
    }

    [Fact]
    public void TryCloseFinalRound_ReturnsNullWhenNoTimeElapsed()
    {
        var match = CreateGame();
        var final = match.TryCloseFinalRound(match.StartedAt);
        final.Should().BeNull();
    }

    [Fact]
    public void TryCloseFinalRound_AfterCloseRoundUsesLastEndAsStart()
    {
        var match = CreateGame();
        var t0 = match.StartedAt;
        var t1 = t0.AddSeconds(20);
        var t2 = t0.AddSeconds(50);
        var r1 = match.CloseRound(t1);
        match.AddScore("yellow", 3, null);
        var final = match.TryCloseFinalRound(t2);
        final.Should().NotBeNull();
        final!.StartedAt.Should().Be(t1);
        final.EndedAt.Should().Be(t2);
        final.DurationSeconds.Should().Be(30);
    }
}
