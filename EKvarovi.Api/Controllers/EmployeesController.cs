using System.Linq.Expressions;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

// Popis svih zaposlenika ne treba Reporteru/Technicianu - vide samo svoj kontekst
// preko drugih endpointa. Izmjene su rezervirane za Admina.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class EmployeesController : ControllerBase
{
    private readonly EKvaroviDbContext _context;

    // Expression (ne obicna metoda) - EF Core je mora prevesti u SQL projekciju,
    // pa se ne moze pozvati obicna C# metoda unutar .Select() nad IQueryable.
    private static readonly Expression<Func<Employee, EmployeeDto>> ToDtoProjection = e => new EmployeeDto
    {
        Id = e.Id,
        FirstName = e.FirstName,
        LastName = e.LastName,
        Email = e.Email,
        LocationId = e.LocationId,
        LocationName = e.Location != null ? e.Location.Name : string.Empty,
        IsReporter = e.IsReporter,
        IsTechnician = e.IsTechnician,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt
    };

    public EmployeesController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> GetEmployees()
    {
        var employees = await _context.Employees
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(employees);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> GetEmployee(int id)
    {
        var employee = await _context.Employees
            .Where(e => e.Id == id)
            .Select(ToDtoProjection)
            .FirstOrDefaultAsync();

        if (employee is null)
        {
            return NotFound();
        }

        return Ok(employee);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee(SaveEmployeeDto dto)
    {
        var validationError = await ValidateAsync(dto);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var employee = new Employee
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            LocationId = dto.LocationId,
            IsReporter = dto.IsReporter,
            IsTechnician = dto.IsTechnician,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var createdDto = await _context.Employees
            .Where(e => e.Id == employee.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, createdDto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateEmployee(int id, SaveEmployeeDto dto)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null)
        {
            return NotFound();
        }

        var validationError = await ValidateAsync(dto);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        employee.FirstName = dto.FirstName;
        employee.LastName = dto.LastName;
        employee.Email = dto.Email;
        employee.LocationId = dto.LocationId;
        employee.IsReporter = dto.IsReporter;
        employee.IsTechnician = dto.IsTechnician;
        employee.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null)
        {
            return NotFound();
        }

        var hasFaultReports = await _context.FaultReports.AnyAsync(fr => fr.ReporterId == id);
        var hasWorkAssignments = await _context.WorkAssignments.AnyAsync(wa => wa.TechnicianId == id);
        if (hasFaultReports || hasWorkAssignments)
        {
            return BadRequest("Zaposlenik s postojećim prijavama kvarova ili nalozima se ne može obrisati - deaktivirajte ga umjesto toga (IsActive = false).");
        }

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<string?> ValidateAsync(SaveEmployeeDto dto)
    {
        var locationExists = await _context.Locations.AnyAsync(l => l.Id == dto.LocationId);
        if (!locationExists)
        {
            return $"Lokacija s Id={dto.LocationId} ne postoji.";
        }

        if (!dto.IsReporter && !dto.IsTechnician)
        {
            return "Zaposlenik mora imati barem jednu ulogu - Prijavitelj ili Izvršitelj.";
        }

        return null;
    }
}
