namespace EKvarovi.Shared.DTOs;

public class InterventionQueryParametersDto
{
    public string? Search { get; set; }

    public int? WorkAssignmentId { get; set; }

    public int? FaultReportId { get; set; }

    public int? InterventionStatusId { get; set; }

    public int? TechnicianId { get; set; }

    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
