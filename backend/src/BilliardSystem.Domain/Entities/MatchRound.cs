using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class MatchRound : Entity
{
    private MatchRound()
    {
    }

    public MatchRound(Guid matchHistoryId, int roundNumber, int whiteScore, int yellowScore, string? winnerName, DateTimeOffset startedAt)
    {
        MatchHistoryId = matchHistoryId;
        RoundNumber = roundNumber;
        WhiteScore = whiteScore;
        YellowScore = yellowScore;
        WinnerName = winnerName;
        StartedAt = startedAt;
    }

    public Guid MatchHistoryId { get; private set; }
    public MatchHistory? MatchHistory { get; private set; }
    public int RoundNumber { get; private set; }
    public int WhiteScore { get; private set; }
    public int YellowScore { get; private set; }
    public string? WinnerName { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset EndedAt { get; private set; } = DateTimeOffset.UtcNow;
    public TimeSpan Duration => EndedAt - StartedAt;
}
