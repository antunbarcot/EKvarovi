using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class ReassignWorkAssignmentDto
{
    [Required]
    public int TechnicianId { get; set; }

    public string? ReassignmentNote { get; set; }
}
