namespace EKvarovi.Shared.Models;

public class WorkAssignment
{
    public int Id { get; set; }
    public int FaultReportId { get; set; }
    public int TechnicianId { get; set; }
    public DateTime AssignedAt { get; set; }
    public int AssignedByAppUserId { get; set; }
    public DateTime? UnassignedAt { get; set; }
    public bool IsActive { get; set; }
    public string? ReassignmentNote { get; set; }

    public FaultReport? FaultReport { get; set; }
    public Employee? Technician { get; set; }
    public AppUser? AssignedByAppUser { get; set; }
    public ICollection<Intervention> Interventions { get; set; } = new List<Intervention>();
}
