using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class CreateFaultReportDto
{
    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int LocationId { get; set; }

    [Required]
    public int ReporterId { get; set; }
}
