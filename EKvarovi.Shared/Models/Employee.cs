namespace EKvarovi.Shared.Models;

public class Employee
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsReporter { get; set; }
    public bool IsTechnician { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public Location? Location { get; set; }
    public ICollection<FaultReport> ReportedFaultReports { get; set; } = new List<FaultReport>();
    public ICollection<WorkAssignment> WorkAssignments { get; set; } = new List<WorkAssignment>();
    public AppUser? AppUser { get; set; }
}
