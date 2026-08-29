using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class AddInterventionMaterialDto
{
    [Required]
    public int MaterialId { get; set; }

    [Required]
    public decimal Quantity { get; set; }
}
