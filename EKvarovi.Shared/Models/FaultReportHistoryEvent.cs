namespace EKvarovi.Shared.Models;

public class FaultReportHistoryEvent
{
    public int Id { get; set; }
    public int FaultReportId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public int? ChangedByAppUserId { get; set; }

    public FaultReport? FaultReport { get; set; }
    public AppUser? ChangedByAppUser { get; set; }
}
