using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class CreateWorkAssignmentDto
{
    [Required]
    public int FaultReportId { get; set; }

    [Required]
    public int TechnicianId { get; set; }
}
