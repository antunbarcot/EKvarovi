using System.Linq.Expressions;
using EKvarovi.Api.Auth;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

// Iste uloge za sve akcije (Technician radi na SVOJOJ intervenciji - ownership
// provjera dolazi kasnije uz /mine), pa je [Authorize] na razini klase.
[ApiController]
[Route("api/interventions")]
[Authorize(Roles = "Admin,Manager,Technician")]
public class InterventionsController : ControllerBase
{
    private const string InterventionStatusUTijeku = "U tijeku";
    private const string InterventionStatusZavrsena = "Završena";
    private const string InterventionStatusNeuspjesna = "Neuspješna";

    private const string FaultStatusURadu = "U radu";
    private const string FaultStatusRijeseno = "Riješeno";

    private const string HistoryEventStatusChanged = "StatusChanged";

    private readonly EKvaroviDbContext _context;

    private static readonly Expression<Func<Intervention, InterventionDto>> ToDtoProjection = i => new InterventionDto
    {
        Id = i.Id,
        WorkAssignmentId = i.WorkAssignmentId,
        FaultReportId = i.WorkAssignment != null ? i.WorkAssignment.FaultReportId : 0,
        FaultReportDescription = i.WorkAssignment != null && i.WorkAssignment.FaultReport != null
            ? i.WorkAssignment.FaultReport.Description
            : string.Empty,
        TechnicianId = i.WorkAssignment != null ? i.WorkAssignment.TechnicianId : 0,
        TechnicianName = i.WorkAssignment != null && i.WorkAssignment.Technician != null
            ? i.WorkAssignment.Technician.FirstName + " " + i.WorkAssignment.Technician.LastName
            : string.Empty,
        InterventionStatusId = i.InterventionStatusId,
        InterventionStatusName = i.InterventionStatus != null ? i.InterventionStatus.Name : string.Empty,
        StartedAt = i.StartedAt,
        EndedAt = i.EndedAt,
        DurationMinutes = i.DurationMinutes,
        Notes = i.Notes,
        Materials = i.InterventionMaterials.Select(im => new InterventionMaterialDto
        {
            MaterialId = im.MaterialId,
            MaterialName = im.Material != null ? im.Material.Name : string.Empty,
            MaterialUnitAbbreviation = im.Material != null && im.Material.MaterialUnit != null
                ? im.Material.MaterialUnit.Abbreviation
                : string.Empty,
            Quantity = im.Quantity
        }).ToList()
    };

    public InterventionsController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<InterventionDto>>> GetInterventions([FromQuery] InterventionQueryParametersDto parameters)
    {
        IQueryable<Intervention> query = _context.Interventions;

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            query = query.Where(i => i.WorkAssignment != null
                && i.WorkAssignment.FaultReport != null
                && i.WorkAssignment.FaultReport.Description.Contains(parameters.Search));
        }

        if (parameters.WorkAssignmentId.HasValue)
        {
            query = query.Where(i => i.WorkAssignmentId == parameters.WorkAssignmentId.Value);
        }

        if (parameters.FaultReportId.HasValue)
        {
            query = query.Where(i => i.WorkAssignment != null && i.WorkAssignment.FaultReportId == parameters.FaultReportId.Value);
        }

        if (parameters.InterventionStatusId.HasValue)
        {
            query = query.Where(i => i.InterventionStatusId == parameters.InterventionStatusId.Value);
        }

        if (parameters.TechnicianId.HasValue)
        {
            query = query.Where(i => i.WorkAssignment != null && i.WorkAssignment.TechnicianId == parameters.TechnicianId.Value);
        }

        if (parameters.DateFrom.HasValue)
        {
            query = query.Where(i => i.StartedAt >= parameters.DateFrom.Value);
        }

        if (parameters.DateTo.HasValue)
        {
            query = query.Where(i => i.StartedAt <= parameters.DateTo.Value);
        }

        query = ApplySorting(query, parameters.SortBy, parameters.SortDescending);

        var interventions = await query
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(interventions);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InterventionDto>> GetIntervention(int id)
    {
        var intervention = await _context.Interventions
            .Where(i => i.Id == id)
            .Select(ToDtoProjection)
            .FirstOrDefaultAsync();

        if (intervention is null)
        {
            return NotFound();
        }

        return Ok(intervention);
    }

    [HttpPost]
    public async Task<ActionResult<InterventionDto>> StartIntervention(StartInterventionDto dto)
    {
        var workAssignment = await _context.WorkAssignments
            .FirstOrDefaultAsync(wa => wa.Id == dto.WorkAssignmentId);

        if (workAssignment is null)
        {
            return NotFound($"Nalog s Id={dto.WorkAssignmentId} ne postoji.");
        }

        var ownershipError = EnsureTechnicianOwnsAssignment(workAssignment.TechnicianId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        if (!workAssignment.IsActive)
        {
            return BadRequest("Intervencija se može pokrenuti samo na aktivnom nalogu.");
        }

        var hasOngoingIntervention = await _context.Interventions
            .Where(i => i.WorkAssignmentId == dto.WorkAssignmentId)
            .AnyAsync(i => i.InterventionStatus!.Name == InterventionStatusUTijeku);
        if (hasOngoingIntervention)
        {
            return BadRequest("Već postoji intervencija u tijeku na ovom nalogu.");
        }

        var uTijekuStatus = await _context.InterventionStatuses.FirstOrDefaultAsync(s => s.Name == InterventionStatusUTijeku);
        if (uTijekuStatus is null)
        {
            return BadRequest($"Status \"{InterventionStatusUTijeku}\" nije pronađen u šifrarniku statusa intervencija.");
        }

        var uRaduStatus = await _context.FaultStatuses.FirstOrDefaultAsync(s => s.Name == FaultStatusURadu);
        if (uRaduStatus is null)
        {
            return BadRequest($"Status \"{FaultStatusURadu}\" nije pronađen u šifrarniku statusa prijava.");
        }

        var faultReport = await _context.FaultReports
            .Include(fr => fr.FaultStatus)
            .FirstOrDefaultAsync(fr => fr.Id == workAssignment.FaultReportId);
        if (faultReport is null)
        {
            return BadRequest("Nalog nije povezan s postojećom prijavom.");
        }

        // Status prijave se biljezi u povijest SAMO kod prve intervencije na ovoj dodjeli -
        // ako je prethodna na istoj dodjeli bila neuspjesna, prijava je vec "U radu" i ovdje
        // se nista stvarno ne mijenja, pa nema smisla dodavati jos jedan StatusChanged zapis.
        var isFirstInterventionOnAssignment = !await _context.Interventions
            .AnyAsync(i => i.WorkAssignmentId == dto.WorkAssignmentId);
        var previousStatusName = faultReport.FaultStatus?.Name;

        var now = DateTime.UtcNow;

        var intervention = new Intervention
        {
            WorkAssignmentId = dto.WorkAssignmentId,
            InterventionStatusId = uTijekuStatus.Id,
            StartedAt = now,
            EndedAt = null,
            DurationMinutes = null,
            Notes = null,
            CreatedAt = now
        };

        _context.Interventions.Add(intervention);
        faultReport.FaultStatusId = uRaduStatus.Id;
        faultReport.UpdatedAt = now;

        if (isFirstInterventionOnAssignment)
        {
            _context.FaultReportHistoryEvents.Add(FaultReportHistoryEvents.Create(
                faultReport.Id, HistoryEventStatusChanged, previousStatusName, uRaduStatus.Name, User.GetAppUserId(), now));
        }

        await _context.SaveChangesAsync();

        var createdDto = await _context.Interventions
            .Where(i => i.Id == intervention.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetIntervention), new { id = intervention.Id }, createdDto);
    }

    [HttpPut("{id:int}/complete")]
    public async Task<IActionResult> CompleteIntervention(int id, CompleteInterventionDto dto)
    {
        var intervention = await _context.Interventions
            .Include(i => i.InterventionStatus)
            .Include(i => i.WorkAssignment)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (intervention is null)
        {
            return NotFound();
        }

        var ownershipError = EnsureTechnicianOwnsAssignment(intervention.WorkAssignment?.TechnicianId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        if (intervention.InterventionStatus?.Name != InterventionStatusUTijeku)
        {
            return BadRequest("Samo intervencija u statusu \"U tijeku\" se može završiti.");
        }

        if (intervention.StartedAt is null)
        {
            return BadRequest("Intervencija nema evidentiran početak.");
        }

        if (string.IsNullOrWhiteSpace(dto.Notes))
        {
            return BadRequest("Završena intervencija mora imati bilješku.");
        }

        var endedAt = dto.EndedAt ?? DateTime.UtcNow;
        var durationMinutes = dto.DurationMinutes ?? (int)Math.Round((endedAt - intervention.StartedAt.Value).TotalMinutes);

        intervention.EndedAt = endedAt;
        intervention.Notes = dto.Notes;
        intervention.DurationMinutes = durationMinutes;

        if (dto.IsSuccessful)
        {
            var zavrsenaStatus = await _context.InterventionStatuses.FirstOrDefaultAsync(s => s.Name == InterventionStatusZavrsena);
            if (zavrsenaStatus is null)
            {
                return BadRequest($"Status \"{InterventionStatusZavrsena}\" nije pronađen u šifrarniku statusa intervencija.");
            }

            var rijesenoStatus = await _context.FaultStatuses.FirstOrDefaultAsync(s => s.Name == FaultStatusRijeseno);
            if (rijesenoStatus is null)
            {
                return BadRequest($"Status \"{FaultStatusRijeseno}\" nije pronađen u šifrarniku statusa prijava.");
            }

            intervention.InterventionStatusId = zavrsenaStatus.Id;

            var faultReport = await _context.FaultReports.FirstOrDefaultAsync(fr => fr.Id == intervention.WorkAssignment!.FaultReportId);
            if (faultReport is not null)
            {
                faultReport.FaultStatusId = rijesenoStatus.Id;
                faultReport.UpdatedAt = DateTime.UtcNow;

                _context.FaultReportHistoryEvents.Add(FaultReportHistoryEvents.Create(
                    faultReport.Id, HistoryEventStatusChanged, FaultStatusURadu, rijesenoStatus.Name, User.GetAppUserId(), faultReport.UpdatedAt));
            }
        }
        else
        {
            var neuspjesnaStatus = await _context.InterventionStatuses.FirstOrDefaultAsync(s => s.Name == InterventionStatusNeuspjesna);
            if (neuspjesnaStatus is null)
            {
                return BadRequest($"Status \"{InterventionStatusNeuspjesna}\" nije pronađen u šifrarniku statusa intervencija.");
            }

            // FaultReport status se namjerno NE mijenja - ostaje "U radu" tako da
            // se na istoj (i dalje aktivnoj) dodjeli može pokrenuti nova intervencija.
            intervention.InterventionStatusId = neuspjesnaStatus.Id;
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:int}/materials")]
    public async Task<ActionResult<InterventionMaterialDto>> AddInterventionMaterial(int id, AddInterventionMaterialDto dto)
    {
        var intervention = await _context.Interventions
            .Include(i => i.InterventionStatus)
            .Include(i => i.WorkAssignment)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (intervention is null)
        {
            return NotFound($"Intervencija s Id={id} ne postoji.");
        }

        var ownershipError = EnsureTechnicianOwnsAssignment(intervention.WorkAssignment?.TechnicianId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        if (intervention.InterventionStatus?.Name != InterventionStatusUTijeku)
        {
            return BadRequest("Materijal se može dodavati samo dok je intervencija u statusu \"U tijeku\".");
        }

        var material = await _context.Materials
            .Include(m => m.MaterialUnit)
            .FirstOrDefaultAsync(m => m.Id == dto.MaterialId);
        if (material is null)
        {
            return NotFound($"Materijal s Id={dto.MaterialId} ne postoji.");
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest("Količina materijala mora biti veća od nule.");
        }

        // Ako materijal već postoji na ovoj intervenciji, količina se zbraja umjesto
        // dupliciranja retka - Quantity je jedini podatak koji nosi InterventionMaterial
        // (nema npr. serije/napomene po retku) pa dva retka za isti materijal ne bi nosila
        // nikakvu dodatnu informaciju, samo bi otežala zbrajanje potrošnje pri izvještavanju.
        var existingRow = await _context.InterventionMaterials
            .FirstOrDefaultAsync(im => im.InterventionId == id && im.MaterialId == dto.MaterialId);

        bool isNewRow = existingRow is null;

        if (existingRow is null)
        {
            existingRow = new InterventionMaterial
            {
                InterventionId = id,
                MaterialId = dto.MaterialId,
                Quantity = dto.Quantity
            };
            _context.InterventionMaterials.Add(existingRow);
        }
        else
        {
            existingRow.Quantity += dto.Quantity;
        }

        await _context.SaveChangesAsync();

        var resultDto = new InterventionMaterialDto
        {
            MaterialId = material.Id,
            MaterialName = material.Name,
            MaterialUnitAbbreviation = material.MaterialUnit?.Abbreviation ?? string.Empty,
            Quantity = existingRow.Quantity
        };

        return isNewRow
            ? CreatedAtAction(nameof(GetIntervention), new { id }, resultDto)
            : Ok(resultDto);
    }

    [HttpDelete("{id:int}/materials/{materialId:int}")]
    public async Task<IActionResult> RemoveInterventionMaterial(int id, int materialId)
    {
        var intervention = await _context.Interventions
            .Include(i => i.InterventionStatus)
            .Include(i => i.WorkAssignment)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (intervention is null)
        {
            return NotFound($"Intervencija s Id={id} ne postoji.");
        }

        var ownershipError = EnsureTechnicianOwnsAssignment(intervention.WorkAssignment?.TechnicianId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        if (intervention.InterventionStatus?.Name != InterventionStatusUTijeku)
        {
            return BadRequest("Materijal se može uklanjati samo dok je intervencija u statusu \"U tijeku\".");
        }

        var row = await _context.InterventionMaterials
            .FirstOrDefaultAsync(im => im.InterventionId == id && im.MaterialId == materialId);
        if (row is null)
        {
            return NotFound($"Materijal s Id={materialId} nije evidentiran na intervenciji {id}.");
        }

        _context.InterventionMaterials.Remove(row);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // "Izvrsitelj smije mijenjati samo svoj aktivni nalog" - Admin/Manager zaobilaze ovu
    // provjeru (vec pokriveno [Authorize] na klasi), za Technician identitet se cita
    // ISKLJUCIVO iz JWT "EmployeeId" claima, nikad iz parametra koji salje klijent.
    private ActionResult? EnsureTechnicianOwnsAssignment(int? assignmentTechnicianId)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Manager"))
        {
            return null;
        }

        var employeeId = User.GetEmployeeId();
        if (employeeId is null || assignmentTechnicianId is null || employeeId != assignmentTechnicianId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Možete raditi samo na svojim dodijeljenim nalozima.");
        }

        return null;
    }

    private static IQueryable<Intervention> ApplySorting(IQueryable<Intervention> query, string? sortBy, bool sortDescending)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return query.OrderByDescending(i => i.StartedAt);
        }

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "startedat" => sortDescending
                ? query.OrderByDescending(i => i.StartedAt)
                : query.OrderBy(i => i.StartedAt),
            "endedat" => sortDescending
                ? query.OrderByDescending(i => i.EndedAt)
                : query.OrderBy(i => i.EndedAt),
            _ => query.OrderByDescending(i => i.StartedAt)
        };
    }
}
