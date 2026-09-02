namespace EKvarovi.Shared.DTOs;

public class UserAdminDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
}
