using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class ReviewFaultReportDto
{
    [Required]
    public int FaultTypeId { get; set; }

    [Required]
    public int FaultPriorityId { get; set; }

    // Obavezan samo ako je odabrani prioritet "Kritican" - provjerava se u
    // kontroleru jer ovisi o vrijednosti FaultPriorityId, ne moze biti
    // izrazeno kao DataAnnotation na ovom polju samom za sebe.
    public DateTime? DueDate { get; set; }
}
