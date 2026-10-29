namespace RaidRoulette.Models;

public sealed class EventReport
{
    private readonly List<string> details = [];
    private readonly List<string> verification = [];

    public EventReport(string eventId, string eventName, string locationId)
    {
        EventId = eventId;
        EventName = eventName;
        LocationId = locationId;
    }

    public string EventId { get; }

    public string EventName { get; }

    public string LocationId { get; }

    public EventReportStatus Status { get; private set; } = EventReportStatus.Pending;

    public string? StatusMessage { get; private set; }

    public IReadOnlyList<string> Details => details;

    public IReadOnlyList<string> Verification => verification;

    public void AddDetail(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            details.Add(message);
        }
    }

    public void AddVerification(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            verification.Add(message);
        }
    }

    public void MarkApplied(string? message = null)
    {
        Status = EventReportStatus.Applied;
        StatusMessage = message;
    }

    public void MarkSkipped(string message)
    {
        Status = EventReportStatus.Skipped;
        StatusMessage = message;
    }

    public void MarkFailed(string message)
    {
        Status = EventReportStatus.Failed;
        StatusMessage = message;
    }
}

public enum EventReportStatus
{
    Pending,
    Applied,
    Skipped,
    Failed
}
