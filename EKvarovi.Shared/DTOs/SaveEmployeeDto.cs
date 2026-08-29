using System.ComponentModel.DataAnnotations;

namespace EKvarovi.Shared.DTOs;

public class SaveEmployeeDto
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public int LocationId { get; set; }

    public bool IsReporter { get; set; }
    public bool IsTechnician { get; set; }
    public bool IsActive { get; set; }
}
