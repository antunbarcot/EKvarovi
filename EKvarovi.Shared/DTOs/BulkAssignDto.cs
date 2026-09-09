using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class BulkAssignDto
{
    [Required]
    [MinLength(1)]
    public List<int> FaultReportIds { get; set; } = new();

    [Required]
    public int TechnicianId { get; set; }
}
