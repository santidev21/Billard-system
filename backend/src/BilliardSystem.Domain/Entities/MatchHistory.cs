using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Enums;

namespace BilliardSystem.Domain.Entities;

public sealed class MatchHistory : Entity
{
    private readonly List<MatchScoreLog> _scoreLogs = [];
    private readonly List<MatchConsumption> _consumptions = [];

    private MatchHistory()
    {
    }

    public MatchHistory(Guid tableId, string whitePlayerName, string yellowPlayerName, decimal hourlyRateSnapshot, Guid? openedByUserId, GameMode gameMode)
    {
        TableId = tableId;
        WhitePlayerName = whitePlayerName;
        YellowPlayerName = yellowPlayerName;
        HourlyRateSnapshot = hourlyRateSnapshot;
        OpenedByUserId = openedByUserId;
        GameMode = gameMode;
    }

    public Guid TableId { get; private set; }
    public BilliardTable? Table { get; private set; }
    public string WhitePlayerName { get; private set; } = string.Empty;
    public string YellowPlayerName { get; private set; } = string.Empty;
    public int WhiteScore { get; private set; }
    public int YellowScore { get; private set; }
    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; private set; }
    public decimal HourlyRateSnapshot { get; private set; }
    public GameMode GameMode { get; private set; } = GameMode.Managed;
    public decimal TableTotal { get; private set; }
    public decimal ConsumptionTotal { get; private set; }
    public decimal GrandTotal { get; private set; }
    public string SystemVersion { get; private set; } = "0.1.0";
    public Guid? OpenedByUserId { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public IReadOnlyCollection<MatchScoreLog> ScoreLogs => _scoreLogs.AsReadOnly();
    public IReadOnlyCollection<MatchConsumption> Consumptions => _consumptions.AsReadOnly();

    public int TotalCarambolas => WhiteScore + YellowScore;

    public MatchScoreLog AddScore(string playerColor, int delta, Guid? userId)
    {
        var color = playerColor.Equals("yellow", StringComparison.OrdinalIgnoreCase)
            ? "Yellow"
            : "White";

        var current = color == "Yellow" ? YellowScore : WhiteScore;
        var resulting = Math.Max(0, current + delta);

        if (color == "Yellow")
        {
            YellowScore = resulting;
        }
        else
        {
            WhiteScore = resulting;
        }

        var scoreLog = new MatchScoreLog(Id, color, delta, resulting, userId);
        _scoreLogs.Add(scoreLog);
        return scoreLog;
    }

    public void RenamePlayer(string playerColor, string newName)
    {
        if (playerColor.Equals("yellow", StringComparison.OrdinalIgnoreCase))
        {
            YellowPlayerName = newName;
        }
        else
        {
            WhitePlayerName = newName;
        }
    }

    public MatchConsumption AddConsumption(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        var consumption = new MatchConsumption(Id, productId, productName, unitPrice, quantity);
        _consumptions.Add(consumption);
        ConsumptionTotal = _consumptions.Sum(item => item.Total);
        return consumption;
    }

    public void Close(DateTimeOffset endedAt, decimal tableTotal, decimal consumptionTotal, Guid? closedByUserId)
    {
        EndedAt = endedAt;
        TableTotal = tableTotal;
        ConsumptionTotal = consumptionTotal;
        GrandTotal = tableTotal + consumptionTotal;
        ClosedByUserId = closedByUserId;
    }
}
