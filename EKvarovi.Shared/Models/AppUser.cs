namespace EKvarovi.Shared.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? EmployeeId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Employee? Employee { get; set; }
    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
    public ICollection<WorkAssignment> AssignedWorkAssignments { get; set; } = new List<WorkAssignment>();
    public ICollection<Attachment> UploadedAttachments { get; set; } = new List<Attachment>();
    public ICollection<FaultReportHistoryEvent> ChangedHistoryEvents { get; set; } = new List<FaultReportHistoryEvent>();
}
