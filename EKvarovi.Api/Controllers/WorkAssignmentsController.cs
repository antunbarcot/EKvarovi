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
[Route("api/work-assignments")]
[Authorize(Roles = "Admin,Manager,Technician")]
public class WorkAssignmentsController : ControllerBase
{
    private const string StatusDodijeljeno = "Dodijeljeno";

    private const string HistoryEventStatusChanged = "StatusChanged";
    private const string HistoryEventAssigned = "Assigned";
    private const string HistoryEventReassigned = "Reassigned";

    private const int SystemAppUserId = 1;

    private readonly EKvaroviDbContext _context;

    private static readonly Expression<Func<WorkAssignment, WorkAssignmentDto>> ToDtoProjection = wa => new WorkAssignmentDto
    {
        Id = wa.Id,
        FaultReportId = wa.FaultReportId,
        FaultReportDescription = wa.FaultReport != null ? wa.FaultReport.Description : string.Empty,
        LocationName = wa.FaultReport != null && wa.FaultReport.Location != null ? wa.FaultReport.Location.Name : string.Empty,
        TechnicianId = wa.TechnicianId,
        TechnicianName = wa.Technician != null ? wa.Technician.FirstName + " " + wa.Technician.LastName : string.Empty,
        AssignedAt = wa.AssignedAt,
        UnassignedAt = wa.UnassignedAt,
        IsActive = wa.IsActive,
        ReassignmentNote = wa.ReassignmentNote
    };

    public WorkAssignmentsController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkAssignmentDto>>> GetWorkAssignments([FromQuery] int? faultReportId, [FromQuery] bool activeOnly = false)
    {
        IQueryable<WorkAssignment> query = _context.WorkAssignments;

        if (faultReportId.HasValue)
        {
            query = query.Where(wa => wa.FaultReportId == faultReportId.Value);
        }

        if (activeOnly)
        {
            query = query.Where(wa => wa.IsActive);
        }

        var workAssignments = await query
            .OrderByDescending(wa => wa.AssignedAt)
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(workAssignments);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Technician")]
    public async Task<ActionResult<List<WorkAssignmentDto>>> GetMyWorkAssignments()
    {
        var employeeId = User.GetEmployeeId();
        if (employeeId is null)
        {
            return BadRequest("Račun nije povezan s izvršiteljem.");
        }

        var workAssignments = await _context.WorkAssignments
            .Where(wa => wa.TechnicianId == employeeId.Value && wa.IsActive)
            .OrderByDescending(wa => wa.AssignedAt)
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(workAssignments);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkAssignmentDto>> GetWorkAssignment(int id)
    {
        var workAssignment = await _context.WorkAssignments
            .Where(wa => wa.Id == id)
            .Select(ToDtoProjection)
            .FirstOrDefaultAsync();

        if (workAssignment is null)
        {
            return NotFound();
        }

        return Ok(workAssignment);
    }

    [HttpGet("active/{faultReportId:int}")]
    public async Task<ActionResult<WorkAssignmentDto>> GetActiveWorkAssignment(int faultReportId)
    {
        var activeAssignment = await _context.WorkAssignments
            .Where(wa => wa.FaultReportId == faultReportId && wa.IsActive)
            .Select(ToDtoProjection)
            .FirstOrDefaultAsync();

        if (activeAssignment is null)
        {
            return NotFound($"Prijava s Id={faultReportId} nema aktivnu dodjelu.");
        }

        return Ok(activeAssignment);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<WorkAssignmentDto>> CreateWorkAssignment(CreateWorkAssignmentDto dto)
    {
        var faultReport = await _context.FaultReports
            .Include(fr => fr.FaultStatus)
            .FirstOrDefaultAsync(fr => fr.Id == dto.FaultReportId);
        if (faultReport is null)
        {
            return NotFound($"Prijava s Id={dto.FaultReportId} ne postoji.");
        }

        var hasActiveAssignment = await _context.WorkAssignments
            .AnyAsync(wa => wa.FaultReportId == dto.FaultReportId && wa.IsActive);
        if (hasActiveAssignment)
        {
            return BadRequest("Prijava već ima aktivnu dodjelu, koristi ponovnu dodjelu.");
        }

        var technician = await _context.Employees.FirstOrDefaultAsync(e => e.Id == dto.TechnicianId);
        if (technician is null || !technician.IsTechnician)
        {
            return BadRequest("Odabrani zaposlenik ne postoji ili nema ulogu Izvršitelj.");
        }

        var dodijeljenoStatus = await _context.FaultStatuses.FirstOrDefaultAsync(s => s.Name == StatusDodijeljeno);
        if (dodijeljenoStatus is null)
        {
            return BadRequest($"Status \"{StatusDodijeljeno}\" nije pronađen u šifrarniku statusa.");
        }

        var now = DateTime.UtcNow;
        var previousStatusName = faultReport.FaultStatus?.Name;

        var workAssignment = new WorkAssignment
        {
            FaultReportId = dto.FaultReportId,
            TechnicianId = dto.TechnicianId,
            AssignedAt = now,
            AssignedByAppUserId = SystemAppUserId,
            UnassignedAt = null,
            IsActive = true,
            ReassignmentNote = null
        };

        _context.WorkAssignments.Add(workAssignment);
        faultReport.FaultStatusId = dodijeljenoStatus.Id;
        faultReport.UpdatedAt = now;

        var appUserId = User.GetAppUserId();
        _context.FaultReportHistoryEvents.AddRange(
            FaultReportHistoryEvents.Create(
                dto.FaultReportId, HistoryEventAssigned, null, $"{technician.FirstName} {technician.LastName}", appUserId, now),
            FaultReportHistoryEvents.Create(
                dto.FaultReportId, HistoryEventStatusChanged, previousStatusName, dodijeljenoStatus.Name, appUserId, now));

        await _context.SaveChangesAsync();

        var createdDto = await _context.WorkAssignments
            .Where(wa => wa.Id == workAssignment.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetWorkAssignment), new { id = workAssignment.Id }, createdDto);
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<BulkAssignResultDto>> BulkAssign(BulkAssignDto dto)
    {
        var technician = await _context.Employees.FirstOrDefaultAsync(e => e.Id == dto.TechnicianId);
        if (technician is null || !technician.IsTechnician)
        {
            return BadRequest("Odabrani zaposlenik ne postoji ili nema ulogu Izvršitelj.");
        }

        var dodijeljenoStatus = await _context.FaultStatuses.FirstOrDefaultAsync(s => s.Name == StatusDodijeljeno);
        if (dodijeljenoStatus is null)
        {
            return BadRequest($"Status \"{StatusDodijeljeno}\" nije pronađen u šifrarniku statusa.");
        }

        var requestedIds = dto.FaultReportIds.Distinct().ToList();

        var faultReports = await _context.FaultReports
            .Include(fr => fr.FaultStatus)
            .Where(fr => requestedIds.Contains(fr.Id))
            .ToListAsync();

        var alreadyAssignedIds = (await _context.WorkAssignments
            .Where(wa => requestedIds.Contains(wa.FaultReportId) && wa.IsActive)
            .Select(wa => wa.FaultReportId)
            .ToListAsync())
            .ToHashSet();

        var now = DateTime.UtcNow;
        var appUserId = User.GetAppUserId();
        var result = new BulkAssignResultDto();

        foreach (var faultReportId in requestedIds)
        {
            var faultReport = faultReports.FirstOrDefault(fr => fr.Id == faultReportId);
            if (faultReport is null)
            {
                result.Skipped.Add(new BulkAssignSkippedDto { FaultReportId = faultReportId, Reason = "Prijava ne postoji." });
                continue;
            }

            if (alreadyAssignedIds.Contains(faultReportId))
            {
                result.Skipped.Add(new BulkAssignSkippedDto { FaultReportId = faultReportId, Reason = "Prijava već ima aktivnu dodjelu." });
                continue;
            }

            var previousStatusName = faultReport.FaultStatus?.Name;

            _context.WorkAssignments.Add(new WorkAssignment
            {
                FaultReportId = faultReportId,
                TechnicianId = dto.TechnicianId,
                AssignedAt = now,
                AssignedByAppUserId = SystemAppUserId,
                UnassignedAt = null,
                IsActive = true,
                ReassignmentNote = null
            });

            faultReport.FaultStatusId = dodijeljenoStatus.Id;
            faultReport.UpdatedAt = now;

            _context.FaultReportHistoryEvents.AddRange(
                FaultReportHistoryEvents.Create(
                    faultReportId, HistoryEventAssigned, null, $"{technician.FirstName} {technician.LastName}", appUserId, now),
                FaultReportHistoryEvents.Create(
                    faultReportId, HistoryEventStatusChanged, previousStatusName, dodijeljenoStatus.Name, appUserId, now));

            result.SuccessCount++;
        }

        await _context.SaveChangesAsync();

        return Ok(result);
    }

    [HttpPost("{faultReportId:int}/reassign")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<WorkAssignmentDto>> ReassignWorkAssignment(int faultReportId, ReassignWorkAssignmentDto dto)
    {
        var currentAssignment = await _context.WorkAssignments
            .FirstOrDefaultAsync(wa => wa.FaultReportId == faultReportId && wa.IsActive);

        if (currentAssignment is null)
        {
            var hasAnyAssignment = await _context.WorkAssignments.AnyAsync(wa => wa.FaultReportId == faultReportId);
            if (!hasAnyAssignment)
            {
                return NotFound($"Prijava s Id={faultReportId} nema nijednu dodjelu - koristi POST api/work-assignments za prvu dodjelu.");
            }

            return BadRequest($"Prijava s Id={faultReportId} trenutno nema aktivnu dodjelu za ponovnu dodjelu.");
        }

        var newTechnician = await _context.Employees.FirstOrDefaultAsync(e => e.Id == dto.TechnicianId);
        if (newTechnician is null || !newTechnician.IsTechnician)
        {
            return BadRequest("Odabrani zaposlenik ne postoji ili nema ulogu Izvršitelj.");
        }

        var oldTechnician = await _context.Employees.FirstOrDefaultAsync(e => e.Id == currentAssignment.TechnicianId);

        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        currentAssignment.IsActive = false;
        currentAssignment.UnassignedAt = now;

        var newAssignment = new WorkAssignment
        {
            FaultReportId = faultReportId,
            TechnicianId = dto.TechnicianId,
            AssignedAt = now,
            AssignedByAppUserId = SystemAppUserId,
            UnassignedAt = null,
            IsActive = true,
            ReassignmentNote = dto.ReassignmentNote
        };

        _context.WorkAssignments.Add(newAssignment);

        var oldTechnicianName = oldTechnician is not null ? $"{oldTechnician.FirstName} {oldTechnician.LastName}" : "Nepoznato";
        _context.FaultReportHistoryEvents.Add(FaultReportHistoryEvents.Create(
            faultReportId, HistoryEventReassigned, oldTechnicianName, $"{newTechnician.FirstName} {newTechnician.LastName}",
            User.GetAppUserId(), now));

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var createdDto = await _context.WorkAssignments
            .Where(wa => wa.Id == newAssignment.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetWorkAssignment), new { id = newAssignment.Id }, createdDto);
    }
}
