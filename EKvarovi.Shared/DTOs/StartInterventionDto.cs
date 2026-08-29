using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class StartInterventionDto
{
    [Required]
    public int WorkAssignmentId { get; set; }
}
