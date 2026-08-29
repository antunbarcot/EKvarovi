namespace EKvarovi.Shared.Models;

public class FaultReport
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public int ReporterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? FaultTypeId { get; set; }
    public int? FaultPriorityId { get; set; }
    public int FaultStatusId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Location? Location { get; set; }
    public Employee? Reporter { get; set; }
    public FaultType? FaultType { get; set; }
    public FaultPriority? FaultPriority { get; set; }
    public FaultStatus? FaultStatus { get; set; }
    public ICollection<WorkAssignment> WorkAssignments { get; set; } = new List<WorkAssignment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
