using System.Linq.Expressions;
using System.Security.Claims;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly EKvaroviDbContext _context;

    private static readonly Expression<Func<AppUser, UserAdminDto>> ToDtoProjection = u => new UserAdminDto
    {
        Id = u.Id,
        Email = u.Email,
        DisplayName = u.DisplayName,
        IsActive = u.IsActive,
        Roles = u.UserRoles.Where(ur => ur.AppRole != null).Select(ur => ur.AppRole!.Name).ToList(),
        EmployeeId = u.EmployeeId,
        EmployeeName = u.Employee != null ? u.Employee.FirstName + " " + u.Employee.LastName : null
    };

    public UsersController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserAdminDto>>> GetUsers()
    {
        var users = await _context.AppUsers
            .OrderBy(u => u.Email)
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserAdminDto>> GetUser(int id)
    {
        var user = await _context.AppUsers
            .Where(u => u.Id == id)
            .Select(ToDtoProjection)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserAdminDto>> CreateUser(SaveUserAdminDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var emailTaken = await _context.AppUsers.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (emailTaken)
        {
            return BadRequest("Korisnik s ovim emailom već postoji.");
        }

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest("Lozinka je obavezna kod kreiranja korisnika.");
        }

        var validationError = await ValidateRolesAndEmployeeAsync(dto);
        if (validationError is not null)
        {
            return validationError;
        }

        var user = new AppUser
        {
            Email = dto.Email.Trim(),
            DisplayName = dto.DisplayName,
            IsActive = dto.IsActive,
            EmployeeId = dto.EmployeeId,
            CreatedAt = DateTime.UtcNow
        };

        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, dto.Password);

        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();

        foreach (var roleId in dto.RoleIds.Distinct())
        {
            _context.AppUserRoles.Add(new AppUserRole { AppUserId = user.Id, AppRoleId = roleId });
        }

        await _context.SaveChangesAsync();

        var createdDto = await _context.AppUsers
            .Where(u => u.Id == user.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, createdDto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, SaveUserAdminDto dto)
    {
        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var emailTaken = await _context.AppUsers.AnyAsync(u => u.Id != id && u.Email.ToLower() == normalizedEmail);
        if (emailTaken)
        {
            return BadRequest("Korisnik s ovim emailom već postoji.");
        }

        var validationError = await ValidateRolesAndEmployeeAsync(dto);
        if (validationError is not null)
        {
            return validationError;
        }

        user.Email = dto.Email.Trim();
        user.DisplayName = dto.DisplayName;
        user.IsActive = dto.IsActive;
        user.EmployeeId = dto.EmployeeId;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            var hasher = new PasswordHasher<AppUser>();
            user.PasswordHash = hasher.HashPassword(user, dto.Password);
        }

        var existingRoles = await _context.AppUserRoles.Where(ur => ur.AppUserId == id).ToListAsync();
        _context.AppUserRoles.RemoveRange(existingRoles);

        foreach (var roleId in dto.RoleIds.Distinct())
        {
            _context.AppUserRoles.Add(new AppUserRole { AppUserId = id, AppRoleId = roleId });
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (currentUserIdClaim is not null && int.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == id)
        {
            return BadRequest("Ne možete deaktivirati vlastiti račun.");
        }

        user.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<ActionResult?> ValidateRolesAndEmployeeAsync(SaveUserAdminDto dto)
    {
        var distinctRoleIds = dto.RoleIds.Distinct().ToList();
        if (distinctRoleIds.Count > 0)
        {
            var existingRoleCount = await _context.AppRoles.CountAsync(r => distinctRoleIds.Contains(r.Id));
            if (existingRoleCount != distinctRoleIds.Count)
            {
                return BadRequest("Jedna ili više odabranih uloga ne postoji.");
            }
        }

        if (dto.EmployeeId.HasValue)
        {
            var employeeExists = await _context.Employees.AnyAsync(e => e.Id == dto.EmployeeId.Value);
            if (!employeeExists)
            {
                return NotFound($"Zaposlenik s Id={dto.EmployeeId.Value} ne postoji.");
            }
        }

        return null;
    }
}
