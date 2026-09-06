using System.Linq.Expressions;
using EKvarovi.Api.Auth;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

[ApiController]
[Route("api/fault-reports")]
[Authorize]
public class FaultReportsController : ControllerBase
{
    private const string StatusZaprimljeno = "Zaprimljeno";
    private const string StatusPregledano = "Pregledano";
    private const string StatusRijeseno = "Riješeno";
    private const string StatusZatvoreno = "Zatvoreno";
    private const string PriorityKritican = "Kritičan";
    private const string InterventionStatusZavrsena = "Završena";

    private readonly EKvaroviDbContext _context;

    // Expression (ne obicna metoda) - EF Core je mora prevesti u SQL projekciju,
    // pa se ne moze pozvati obicna C# metoda unutar .Select() nad IQueryable.
    // Internal (ne private) - ponovno je koristi DashboardController za "zadnjih 5 prijava".
    internal static readonly Expression<Func<FaultReport, FaultReportDto>> ToDtoProjection = fr => new FaultReportDto
    {
        Id = fr.Id,
        Description = fr.Description,
        CreatedAt = fr.CreatedAt,
        LocationId = fr.LocationId,
        LocationName = fr.Location != null ? fr.Location.Name : string.Empty,
        ReporterId = fr.ReporterId,
        ReporterName = fr.Reporter != null ? fr.Reporter.FirstName + " " + fr.Reporter.LastName : string.Empty,
        FaultTypeId = fr.FaultTypeId,
        FaultTypeName = fr.FaultType != null ? fr.FaultType.Name : "Nije određeno",
        FaultPriorityId = fr.FaultPriorityId,
        FaultPriorityName = fr.FaultPriority != null ? fr.FaultPriority.Name : "Nije određeno",
        FaultStatusId = fr.FaultStatusId,
        FaultStatusName = fr.FaultStatus != null ? fr.FaultStatus.Name : string.Empty,
        DueDate = fr.DueDate,
        UpdatedAt = fr.UpdatedAt
    };

    public FaultReportsController(EKvaroviDbContext context)
    {
        _context = context;
    }

    // Opci pregled svih prijava (sa svih lokacija) - Reporter/Technician namjerno
    // iskljuceni, oni koriste /mine endpoint (dolazi u sljedecem koraku).
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<List<FaultReportDto>>> GetFaultReports([FromQuery] FaultReportQueryParametersDto parameters)
    {
        IQueryable<FaultReport> query = _context.FaultReports;

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            query = query.Where(fr => fr.Description.Contains(parameters.Search));
        }

        if (parameters.LocationId.HasValue)
        {
            query = query.Where(fr => fr.LocationId == parameters.LocationId.Value);
        }

        if (parameters.FaultTypeId.HasValue)
        {
            query = query.Where(fr => fr.FaultTypeId == parameters.FaultTypeId.Value);
        }

        if (parameters.FaultPriorityId.HasValue)
        {
            query = query.Where(fr => fr.FaultPriorityId == parameters.FaultPriorityId.Value);
        }

        if (parameters.FaultStatusId.HasValue)
        {
            query = query.Where(fr => fr.FaultStatusId == parameters.FaultStatusId.Value);
        }

        if (parameters.DateFrom.HasValue)
        {
            query = query.Where(fr => fr.CreatedAt >= parameters.DateFrom.Value);
        }

        if (parameters.DateTo.HasValue)
        {
            query = query.Where(fr => fr.CreatedAt <= parameters.DateTo.Value);
        }

        query = ApplySorting(query, parameters.SortBy, parameters.SortDescending);

        var faultReports = await query
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(faultReports);
    }

    // Identitet se cita ISKLJUCIVO iz JWT "EmployeeId" claima, nikad iz parametra koji
    // salje klijent - inace bi Reporter mogao poslati tudi Id i vidjeti tude prijave.
    [HttpGet("mine")]
    [Authorize(Roles = "Reporter")]
    public async Task<ActionResult<List<FaultReportDto>>> GetMyFaultReports()
    {
        var employeeId = User.GetEmployeeId();
        if (employeeId is null)
        {
            return BadRequest("Račun nije povezan s prijaviteljem.");
        }

        var faultReports = await _context.FaultReports
            .Where(fr => fr.ReporterId == employeeId.Value)
            .OrderByDescending(fr => fr.CreatedAt)
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(faultReports);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<FaultReportDto>> GetFaultReport(int id)
    {
        var faultReport = await _context.FaultReports
            .Where(fr => fr.Id == id)
            .Select(ToDtoProjection)
            .FirstOrDefaultAsync();

        if (faultReport is null)
        {
            return NotFound();
        }

        return Ok(faultReport);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Reporter")]
    public async Task<ActionResult<FaultReportDto>> CreateFaultReport(CreateFaultReportDto dto)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == dto.LocationId);
        if (location is null || !location.IsActive)
        {
            return BadRequest("Prijava mora pripadati aktivnoj lokaciji.");
        }

        var reporter = await _context.Employees.FirstOrDefaultAsync(e => e.Id == dto.ReporterId);
        if (reporter is null || !reporter.IsReporter)
        {
            return BadRequest("Odabrani zaposlenik ne postoji ili nema ulogu Prijavitelj.");
        }

        var zaprimljenoStatus = await _context.FaultStatuses.FirstOrDefaultAsync(s => s.Name == StatusZaprimljeno);
        if (zaprimljenoStatus is null)
        {
            return BadRequest($"Status \"{StatusZaprimljeno}\" nije pronađen u šifrarniku statusa.");
        }

        var now = DateTime.UtcNow;

        var faultReport = new FaultReport
        {
            LocationId = dto.LocationId,
            ReporterId = dto.ReporterId,
            Title = dto.Description.Length > 200 ? dto.Description[..200] : dto.Description,
            Description = dto.Description,
            FaultStatusId = zaprimljenoStatus.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.FaultReports.Add(faultReport);
        await _context.SaveChangesAsync();

        var createdDto = await _context.FaultReports
            .Where(fr => fr.Id == faultReport.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetFaultReport), new { id = faultReport.Id }, createdDto);
    }

    [HttpPut("{id:int}/review")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ReviewFaultReport(int id, ReviewFaultReportDto dto)
    {
        var faultReport = await _context.FaultReports
            .Include(fr => fr.FaultStatus)
            .FirstOrDefaultAsync(fr => fr.Id == id);

        if (faultReport is null)
        {
            return NotFound();
        }

        var faultType = await _context.FaultTypes.FirstOrDefaultAsync(t => t.Id == dto.FaultTypeId);
        if (faultType is null)
        {
            return BadRequest($"FaultType s Id={dto.FaultTypeId} ne postoji.");
        }

        var faultPriority = await _context.FaultPriorities.FirstOrDefaultAsync(p => p.Id == dto.FaultPriorityId);
        if (faultPriority is null)
        {
            return BadRequest($"FaultPriority s Id={dto.FaultPriorityId} ne postoji.");
        }

        if (faultPriority.Name == PriorityKritican && dto.DueDate is null)
        {
            return BadRequest("Kritična prijava mora imati rok.");
        }

        if (faultReport.FaultStatus?.Name != StatusZaprimljeno)
        {
            return BadRequest("Pregled se može provesti samo nad prijavom u statusu \"Zaprimljeno\".");
        }

        var pregledanoStatus = await _context.FaultStatuses.FirstOrDefaultAsync(s => s.Name == StatusPregledano);
        if (pregledanoStatus is null)
        {
            return BadRequest($"Status \"{StatusPregledano}\" nije pronađen u šifrarniku statusa.");
        }

        faultReport.FaultTypeId = dto.FaultTypeId;
        faultReport.FaultPriorityId = dto.FaultPriorityId;
        faultReport.DueDate = dto.DueDate;
        faultReport.FaultStatusId = pregledanoStatus.Id;
        faultReport.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Zavrsni korak toka: Upravitelj zatvara prijavu tek NAKON provjere da je uspjesno
    // rijesena. Obje provjere ispod (status Rijeseno + postojanje zavrsene intervencije)
    // su, uz ispravan tok kroz UI, redundantne jedna drugoj - ali API ne smije
    // pretpostaviti da je stanje uvijek doslo kroz ocekivani put, pa provjerava oboje eksplicitno.
    [HttpPut("{id:int}/close")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CloseFaultReport(int id)
    {
        var faultReport = await _context.FaultReports
            .Include(fr => fr.FaultStatus)
            .FirstOrDefaultAsync(fr => fr.Id == id);

        if (faultReport is null)
        {
            return NotFound();
        }

        if (faultReport.FaultStatus?.Name != StatusRijeseno)
        {
            return BadRequest("Prijava mora biti u statusu Riješeno prije zatvaranja.");
        }

        var hasSuccessfulIntervention = await _context.Interventions
            .AnyAsync(i => i.WorkAssignment!.FaultReportId == id && i.InterventionStatus!.Name == InterventionStatusZavrsena);

        if (!hasSuccessfulIntervention)
        {
            return BadRequest("Prijava se ne može zatvoriti bez uspješno završene intervencije.");
        }

        var zatvorenoStatus = await _context.FaultStatuses.FirstOrDefaultAsync(s => s.Name == StatusZatvoreno);
        if (zatvorenoStatus is null)
        {
            return BadRequest($"Status \"{StatusZatvoreno}\" nije pronađen u šifrarniku statusa.");
        }

        faultReport.FaultStatusId = zatvorenoStatus.Id;
        faultReport.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFaultReport(int id)
    {
        var faultReport = await _context.FaultReports
            .Include(fr => fr.FaultStatus)
            .FirstOrDefaultAsync(fr => fr.Id == id);

        if (faultReport is null)
        {
            return NotFound();
        }

        // Eksplicitna zastita, neovisna o provjeri dodjela ispod: zatvorena prijava se
        // NIKAD ne smije obrisati, bez obzira kakvo joj je stanje WorkAssignments (iako bi
        // zatvorena prijava gotovo sigurno vec imala dodjele i tako bila blokirana i ispod).
        if (faultReport.FaultStatus?.Name == StatusZatvoreno)
        {
            return BadRequest("Zatvorene prijave se ne mogu brisati.");
        }

        var hasWorkAssignments = await _context.WorkAssignments.AnyAsync(wa => wa.FaultReportId == id);
        if (hasWorkAssignments)
        {
            return BadRequest("Prijava s dodjelama/intervencijama se ne može obrisati.");
        }

        _context.FaultReports.Remove(faultReport);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static IQueryable<FaultReport> ApplySorting(IQueryable<FaultReport> query, string? sortBy, bool sortDescending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return query.OrderByDescending(fr => fr.CreatedAt);
        }

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "createdat" => sortDescending
                ? query.OrderByDescending(fr => fr.CreatedAt)
                : query.OrderBy(fr => fr.CreatedAt),
            "duedate" => sortDescending
                ? query.OrderByDescending(fr => fr.DueDate)
                : query.OrderBy(fr => fr.DueDate),
            "priority" => sortDescending
                ? query.OrderByDescending(fr => fr.FaultPriority!.SortOrder)
                : query.OrderBy(fr => fr.FaultPriority!.SortOrder),
            _ => query.OrderByDescending(fr => fr.CreatedAt)
        };
    }
}
