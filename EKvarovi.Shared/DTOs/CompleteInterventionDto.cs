using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class CompleteInterventionDto
{
    // Ako je null, API postavlja EndedAt = DateTime.UtcNow.
    public DateTime? EndedAt { get; set; }

    // Obavezno - "Završena intervencija mora imati bilješku".
    [Required]
    public string Notes { get; set; } = string.Empty;

    public bool IsSuccessful { get; set; }

    // Ako je null, API računa iz (EndedAt - StartedAt) u minutama.
    public int? DurationMinutes { get; set; }
}
