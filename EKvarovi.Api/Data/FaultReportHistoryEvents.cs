using EKvarovi.Shared.Models;

namespace EKvarovi.Api.Data;

// Zajednicka tvornica FaultReportHistoryEvent zapisa koju koriste FaultReports/
// WorkAssignments/Interventions kontroleri - samo gradi entitet, pozivatelj ga dodaje
// u context i poziva SaveChangesAsync (obicno zajedno s ostalim promjenama iz iste akcije).
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
