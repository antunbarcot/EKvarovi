namespace EKvarovi.Shared.DTOs;

public class FaultReportQueryParametersDto
{
    public string? Search { get; set; }
    public int? LocationId { get; set; }
    public int? FaultTypeId { get; set; }
    public int? FaultPriorityId { get; set; }
    public int? FaultStatusId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public bool? DueSoon { get; set; }

    public bool? UnassignedOnly { get; set; }

    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}
