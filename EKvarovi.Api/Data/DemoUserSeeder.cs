using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Data;

public static class DemoUserSeeder
{
    private const string DemoPassword = "Lozinka123!";

    private const string RoleAdmin = "Admin";
    private const string RoleManager = "Manager";
    private const string RoleTechnician = "Technician";
    private const string RoleReporter = "Reporter";

    public static async Task SeedAsync(EKvaroviDbContext context)
    {
        await SeedUserAsync(context, "admin@ekvarovi.hr", "Administrator", RoleAdmin, employeeId: null);
        await SeedUserAsync(context, "manager@ekvarovi.hr", "Upravitelj", RoleManager, employeeId: null);

        var technicianEmployee = await context.Employees
            .FirstOrDefaultAsync(e => e.Email == DemoDataSeeder.TechnicianMarkerEmail);

        await SeedUserAsync(
            context,
            "tehnicar@ekvarovi.hr",
            technicianEmployee is not null ? $"{technicianEmployee.FirstName} {technicianEmployee.LastName}".Trim() : "Tehničar",
            RoleTechnician,
            technicianEmployee?.Id);

        var reporterEmployee = await context.Employees
            .FirstOrDefaultAsync(e => e.Email == DemoDataSeeder.ReporterMarkerEmail);

        await SeedUserAsync(
            context,
            "prijavitelj@ekvarovi.hr",
            reporterEmployee is not null ? $"{reporterEmployee.FirstName} {reporterEmployee.LastName}".Trim() : "Prijavitelj",
            RoleReporter,
            reporterEmployee?.Id);
    }

    private static async Task SeedUserAsync(EKvaroviDbContext context, string email, string displayName, string roleName, int? employeeId)
    {
        var alreadyExists = await context.AppUsers.AnyAsync(u => u.Email == email);
        if (alreadyExists)
        {
            return;
        }

        var role = await context.AppRoles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role is null)
        {
            return;
        }

        var user = new AppUser
        {
            Email = email,
            DisplayName = displayName,
            IsActive = true,
            EmployeeId = employeeId,
            CreatedAt = DateTime.UtcNow
        };

        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, DemoPassword);

        context.AppUsers.Add(user);
        await context.SaveChangesAsync();

        context.AppUserRoles.Add(new AppUserRole { AppUserId = user.Id, AppRoleId = role.Id });
        await context.SaveChangesAsync();
    }
}
