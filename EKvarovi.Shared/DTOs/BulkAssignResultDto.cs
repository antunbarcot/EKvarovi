namespace EKvarovi.Shared.DTOs;

public class BulkAssignResultDto
{
    public int SuccessCount { get; set; }
    public List<BulkAssignSkippedDto> Skipped { get; set; } = new();
}

public class BulkAssignSkippedDto
{
    public int FaultReportId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
