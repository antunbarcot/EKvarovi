using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class FaultReportSuggestionRequestDto
{
    [Required]
    public string Description { get; set; } = string.Empty;
}
