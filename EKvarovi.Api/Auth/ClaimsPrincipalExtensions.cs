using System.Security.Claims;

namespace EKvarovi.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static int? GetEmployeeId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("EmployeeId")?.Value;
        return claim is not null && int.TryParse(claim, out var employeeId) ? employeeId : null;
    }

    public static int? GetAppUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && int.TryParse(claim, out var appUserId) ? appUserId : null;
    }
}
