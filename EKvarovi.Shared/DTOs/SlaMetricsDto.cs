namespace EKvarovi.Shared.DTOs;

public class SlaMetricsDto
{
    public double? OnTimeResolutionPercentage { get; set; }
    public List<LocationSlaDto> LocationBreakdown { get; set; } = new();
    public List<FaultTypeSlaDto> FaultTypeBreakdown { get; set; } = new();
}
