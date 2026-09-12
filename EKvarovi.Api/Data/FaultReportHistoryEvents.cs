using EKvarovi.Shared.Models;

namespace EKvarovi.Api.Data;

public static class FaultReportHistoryEvents
{
    public static FaultReportHistoryEvent Create(
        int faultReportId,
        string eventType,
        string? oldValue,
        string newValue,
        int? changedByAppUserId,
        DateTime changedAt)
    {
        return new FaultReportHistoryEvent
        {
            FaultReportId = faultReportId,
            EventType = eventType,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAt = changedAt,
            ChangedByAppUserId = changedByAppUserId
        };
    }
}
