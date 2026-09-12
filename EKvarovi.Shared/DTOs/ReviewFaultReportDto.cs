using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class ReviewFaultReportDto
{
    [Required]
    public int FaultTypeId { get; set; }

    [Required]
    public int FaultPriorityId { get; set; }

    public DateTime? DueDate { get; set; }
}
