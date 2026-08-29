namespace EKvarovi.Shared.DTOs;

public class EmployeeDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public bool IsReporter { get; set; }
    public bool IsTechnician { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
