using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class SaveLocationDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required]
    public int LocationTypeId { get; set; }

    public bool IsActive { get; set; }
}
