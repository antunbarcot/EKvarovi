using System.Security.Claims;

namespace EKvarovi.Api.Auth;

// Zajednicko citanje "EmployeeId" custom claima (postavljenog u AuthController pri
// izdavanju JWT tokena) - koriste ga svi /mine endpointi i ownership provjere, da se
// identitet UVIJEK cita iz tokena, nikad iz parametra koji salje klijent.
public static class ClaimsPrincipalExtensions
{
    public static int? GetEmployeeId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("EmployeeId")?.Value;
        return claim is not null && int.TryParse(claim, out var employeeId) ? employeeId : null;
    }

    // AppUserId (za razliku od EmployeeId) identificira RACUN koji je izveo akciju,
    // neovisno o tome ima li taj racun povezani Employee profil - koristi se za
    // ChangedByAppUserId na FaultReportHistoryEvent zapisima.
    public static int? GetAppUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null && int.TryParse(claim, out var appUserId) ? appUserId : null;
    }
}
