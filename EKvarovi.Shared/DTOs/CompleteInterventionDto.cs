using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class CompleteInterventionDto
{
    public DateTime? EndedAt { get; set; }

    [Required]
    public string Notes { get; set; } = string.Empty;

    public bool IsSuccessful { get; set; }

    public int? DurationMinutes { get; set; }
}
