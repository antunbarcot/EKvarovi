namespace EKvarovi.Shared.DTOs;

public class DashboardDto
{
    public int OpenFaultReportsCount { get; set; }
    public int CriticalFaultReportsCount { get; set; }
    public int OverdueFaultReportsCount { get; set; }
    public int UpcomingDueFaultReportsCount { get; set; }
    public int UnassignedFaultReportsCount { get; set; }
    public int ActiveInterventionsCount { get; set; }
    public double? AverageResolutionTimeHours { get; set; }
    public List<FaultReportDto> RecentFaultReports { get; set; } = new();
}
