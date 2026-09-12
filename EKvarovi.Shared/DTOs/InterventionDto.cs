namespace EKvarovi.Shared.DTOs;

public class InterventionDto
{
    public int Id { get; set; }
    public int WorkAssignmentId { get; set; }

    public int FaultReportId { get; set; }
    public string FaultReportDescription { get; set; } = string.Empty;

    public int TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;

    public int InterventionStatusId { get; set; }
    public string InterventionStatusName { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Notes { get; set; }

    public List<InterventionMaterialDto> Materials { get; set; } = new();
}
