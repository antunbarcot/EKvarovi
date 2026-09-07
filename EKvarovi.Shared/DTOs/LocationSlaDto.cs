namespace EKvarovi.Shared.DTOs;

public class LocationSlaDto
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int TotalReports { get; set; }
    public double? AverageResolutionHours { get; set; }
    public double? OnTimePercentage { get; set; }
}
