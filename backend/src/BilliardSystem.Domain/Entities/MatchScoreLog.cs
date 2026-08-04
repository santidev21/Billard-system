using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class MatchScoreLog : Entity
{
    private MatchScoreLog()
    {
    }

    public MatchScoreLog(Guid matchHistoryId, string playerColor, int delta, int resultingScore, Guid? userId)
    {
        MatchHistoryId = matchHistoryId;
        PlayerColor = playerColor;
        Delta = delta;
        ResultingScore = resultingScore;
        UserId = userId;
    }

    public Guid MatchHistoryId { get; private set; }
    public MatchHistory? MatchHistory { get; private set; }
    public string PlayerColor { get; private set; } = string.Empty;
    public int Delta { get; private set; }
    public int ResultingScore { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
