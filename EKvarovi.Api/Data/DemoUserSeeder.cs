using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Data;

// Seed demo AppUser racuna se namjerno radi u kodu pri startu aplikacije (ne kroz
// HasData u migraciji): (1) PasswordHasher<AppUser> treba pozvati u runtimeu da izracuna
// hash, HasData zahtijeva staticku vrijednost poznatu unaprijed; (2) tehnicar/prijavitelj
// racuni se trebaju povezati s POSTOJECIM Employee zapisom ako postoji, a Employee tablica
// se puni rucno kroz UI (nema HasData seed za nju), pa taj Id nije poznat u vrijeme migracije.
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
            .Where(e => e.IsTechnician && e.IsActive)
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync();

        await SeedUserAsync(
            context,
            "tehnicar@ekvarovi.hr",
            technicianEmployee is not null ? $"{technicianEmployee.FirstName} {technicianEmployee.LastName}".Trim() : "Tehničar",
            RoleTechnician,
            technicianEmployee?.Id);

        var reporterEmployee = await context.Employees
            .Where(e => e.IsReporter && e.IsActive)
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync();

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
