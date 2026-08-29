namespace EKvarovi.Shared.Models;

public class AppUserRole
{
    public int AppUserId { get; set; }
    public int AppRoleId { get; set; }

    public AppUser? AppUser { get; set; }
    public AppRole? AppRole { get; set; }
}
