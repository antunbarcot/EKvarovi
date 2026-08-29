using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class SaveMaterialDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int MaterialUnitId { get; set; }

    public bool IsActive { get; set; }
}
