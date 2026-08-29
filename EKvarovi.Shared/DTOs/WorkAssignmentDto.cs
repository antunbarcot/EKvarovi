namespace EKvarovi.Shared.DTOs;

public class WorkAssignmentDto
{
    public int Id { get; set; }
    public int FaultReportId { get; set; }
    public string FaultReportDescription { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;

    public int TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public bool IsActive { get; set; }
    public string? ReassignmentNote { get; set; }
}
