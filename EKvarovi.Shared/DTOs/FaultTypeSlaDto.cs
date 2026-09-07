namespace EKvarovi.Shared.DTOs;

public class FaultTypeSlaDto
{
    public int FaultTypeId { get; set; }
    public string FaultTypeName { get; set; } = string.Empty;
    public int TotalReports { get; set; }
    public double? AverageResolutionHours { get; set; }
    public double? OnTimePercentage { get; set; }
}
