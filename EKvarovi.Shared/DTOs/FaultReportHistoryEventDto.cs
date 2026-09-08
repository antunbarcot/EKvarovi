namespace EKvarovi.Shared.DTOs;

public class FaultReportHistoryEventDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
}
