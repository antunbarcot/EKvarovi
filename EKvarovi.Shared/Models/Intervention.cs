namespace EKvarovi.Shared.Models;

public class Intervention
{
    public int Id { get; set; }
    public int WorkAssignmentId { get; set; }
    public int InterventionStatusId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public WorkAssignment? WorkAssignment { get; set; }
    public InterventionStatus? InterventionStatus { get; set; }
    public ICollection<InterventionMaterial> InterventionMaterials { get; set; } = new List<InterventionMaterial>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
