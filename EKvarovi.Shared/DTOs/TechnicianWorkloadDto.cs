namespace EKvarovi.Shared.DTOs;

public class TechnicianWorkloadDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ActiveAssignmentsCount { get; set; }
}
