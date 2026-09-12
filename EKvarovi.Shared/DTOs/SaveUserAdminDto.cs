namespace EKvarovi.Shared.DTOs;

public class SaveUserAdminDto
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public string? Password { get; set; }

    public bool IsActive { get; set; }
    public List<int> RoleIds { get; set; } = new();
    public int? EmployeeId { get; set; }
}
