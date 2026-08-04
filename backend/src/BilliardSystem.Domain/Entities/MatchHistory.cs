using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class MatchHistory : Entity
{
    private readonly List<MatchScoreLog> _scoreLogs = [];
    private readonly List<MatchConsumption> _consumptions = [];

    private MatchHistory()
    {
    }

    public MatchHistory(Guid tableId, string whitePlayerName, string yellowPlayerName, decimal hourlyRateSnapshot, Guid? openedByUserId)
    {
        TableId = tableId;
        WhitePlayerName = whitePlayerName;
        YellowPlayerName = yellowPlayerName;
        HourlyRateSnapshot = hourlyRateSnapshot;
        OpenedByUserId = openedByUserId;
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
    public decimal TableTotal { get; private set; }
    public decimal ConsumptionTotal { get; private set; }
    public decimal GrandTotal { get; private set; }
    public string SystemVersion { get; private set; } = "0.1.0";
    public Guid? OpenedByUserId { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public IReadOnlyCollection<MatchScoreLog> ScoreLogs => _scoreLogs.AsReadOnly();
    public IReadOnlyCollection<MatchConsumption> Consumptions => _consumptions.AsReadOnly();

    public void Close(DateTimeOffset endedAt, decimal tableTotal, decimal consumptionTotal, Guid? closedByUserId)
    {
        EndedAt = endedAt;
        TableTotal = tableTotal;
        ConsumptionTotal = consumptionTotal;
        GrandTotal = tableTotal + consumptionTotal;
        ClosedByUserId = closedByUserId;
    }
}
