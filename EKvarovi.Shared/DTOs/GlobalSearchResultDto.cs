namespace EKvarovi.Shared.DTOs;

public class GlobalSearchResultDto
{
    public List<LookupDto> FaultReports { get; set; } = new();
    public List<LookupDto> Locations { get; set; } = new();
    public List<LookupDto> Employees { get; set; } = new();
}
