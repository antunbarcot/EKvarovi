using EKvarovi.Api.Auth;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private const string StatusPregledano = "Pregledano";
    private const string StatusRijeseno = "Riješeno";
    private const string StatusZatvoreno = "Zatvoreno";
    private const string PriorityKritican = "Kritičan";
    private const string InterventionStatusUTijeku = "U tijeku";
    private const string InterventionStatusZavrsena = "Završena";

    private readonly EKvaroviDbContext _context;

    public DashboardController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<DashboardDto>> GetDashboard()
    {
        var now = DateTime.UtcNow;

        var pregledanoSortOrder = await _context.FaultStatuses
            .Where(s => s.Name == StatusPregledano)
            .Select(s => s.SortOrder)
            .FirstOrDefaultAsync();

        var openCount = await _context.FaultReports
            .CountAsync(fr => fr.FaultStatus!.Name != StatusZatvoreno);

        var criticalCount = await _context.FaultReports
            .CountAsync(fr => fr.FaultPriority!.Name == PriorityKritican && fr.FaultStatus!.Name != StatusZatvoreno);

        var overdueCount = await _context.FaultReports
            .CountAsync(fr => fr.DueDate != null && fr.DueDate < now
                && fr.FaultStatus!.Name != StatusZatvoreno && fr.FaultStatus!.Name != StatusRijeseno);

        var upcomingDueCount = await _context.FaultReports
            .CountAsync(fr => fr.DueDate != null && fr.DueDate >= now && fr.DueDate <= now.AddHours(24)
                && fr.FaultStatus!.Name != StatusZatvoreno && fr.FaultStatus!.Name != StatusRijeseno);

        var unassignedCount = await _context.FaultReports
            .CountAsync(fr => fr.FaultStatus!.SortOrder >= pregledanoSortOrder
                && !fr.WorkAssignments.Any(wa => wa.IsActive));

        var activeInterventionsCount = await _context.Interventions
            .CountAsync(i => i.InterventionStatus!.Name == InterventionStatusUTijeku);

        var recentFaultReports = await _context.FaultReports
            .OrderByDescending(fr => fr.CreatedAt)
            .Take(5)
            .Select(FaultReportsController.ToDtoProjection)
            .ToListAsync();

        var resolutionTimes = await _context.FaultReports
            .Where(fr => fr.FaultStatus!.Name == StatusRijeseno || fr.FaultStatus!.Name == StatusZatvoreno)
            .Select(fr => new
            {
                fr.CreatedAt,
                LastSuccessEndedAt = fr.WorkAssignments
                    .SelectMany(wa => wa.Interventions)
                    .Where(i => i.InterventionStatus!.Name == InterventionStatusZavrsena && i.EndedAt != null)
                    .OrderByDescending(i => i.EndedAt)
                    .Select(i => i.EndedAt)
                    .FirstOrDefault()
            })
            .Where(x => x.LastSuccessEndedAt != null)
            .ToListAsync();

        double? averageResolutionTimeHours = resolutionTimes.Count > 0
            ? resolutionTimes.Average(x => (x.LastSuccessEndedAt!.Value - x.CreatedAt).TotalHours)
            : null;

        var dashboard = new DashboardDto
        {
            OpenFaultReportsCount = openCount,
            CriticalFaultReportsCount = criticalCount,
            OverdueFaultReportsCount = overdueCount,
            UpcomingDueFaultReportsCount = upcomingDueCount,
            UnassignedFaultReportsCount = unassignedCount,
            ActiveInterventionsCount = activeInterventionsCount,
            AverageResolutionTimeHours = averageResolutionTimeHours,
            RecentFaultReports = recentFaultReports
        };

        return Ok(dashboard);
    }

    [HttpGet("sla")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<SlaMetricsDto>> GetSlaMetrics()
    {
        var locationTotals = await _context.FaultReports
            .GroupBy(fr => new { fr.LocationId, LocationName = fr.Location!.Name })
            .Select(g => new { g.Key.LocationId, g.Key.LocationName, TotalReports = g.Count() })
            .ToListAsync();

        var faultTypeTotals = await _context.FaultReports
            .Where(fr => fr.FaultTypeId != null)
            .GroupBy(fr => new { FaultTypeId = fr.FaultTypeId!.Value, FaultTypeName = fr.FaultType!.Name })
            .Select(g => new { g.Key.FaultTypeId, g.Key.FaultTypeName, TotalReports = g.Count() })
            .ToListAsync();

        var resolutionFacts = await _context.FaultReports
            .Where(fr => fr.FaultStatus!.Name == StatusRijeseno || fr.FaultStatus!.Name == StatusZatvoreno)
            .Select(fr => new
            {
                fr.LocationId,
                fr.FaultTypeId,
                fr.CreatedAt,
                fr.DueDate,
                LastSuccessEndedAt = fr.WorkAssignments
                    .SelectMany(wa => wa.Interventions)
                    .Where(i => i.InterventionStatus!.Name == InterventionStatusZavrsena && i.EndedAt != null)
                    .OrderByDescending(i => i.EndedAt)
                    .Select(i => i.EndedAt)
                    .FirstOrDefault()
            })
            .Where(x => x.LastSuccessEndedAt != null)
            .ToListAsync();

        var locationFacts = resolutionFacts.GroupBy(x => x.LocationId).ToDictionary(g => g.Key, g => g.ToList());
        var faultTypeFacts = resolutionFacts
            .Where(x => x.FaultTypeId.HasValue)
            .GroupBy(x => x.FaultTypeId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var locationBreakdown = locationTotals
            .Select(l =>
            {
                var facts = locationFacts.GetValueOrDefault(l.LocationId) ?? new();
                return new LocationSlaDto
                {
                    LocationId = l.LocationId,
                    LocationName = l.LocationName,
                    TotalReports = l.TotalReports,
                    AverageResolutionHours = AverageResolutionHours(facts.Select(f => (f.CreatedAt, f.LastSuccessEndedAt))),
                    OnTimePercentage = OnTimePercentage(facts.Select(f => (f.DueDate, f.LastSuccessEndedAt)))
                };
            })
            .OrderBy(l => l.LocationName)
            .ToList();

        var faultTypeBreakdown = faultTypeTotals
            .Select(ft =>
            {
                var facts = faultTypeFacts.GetValueOrDefault(ft.FaultTypeId) ?? new();
                return new FaultTypeSlaDto
                {
                    FaultTypeId = ft.FaultTypeId,
                    FaultTypeName = ft.FaultTypeName,
                    TotalReports = ft.TotalReports,
                    AverageResolutionHours = AverageResolutionHours(facts.Select(f => (f.CreatedAt, f.LastSuccessEndedAt))),
                    OnTimePercentage = OnTimePercentage(facts.Select(f => (f.DueDate, f.LastSuccessEndedAt)))
                };
            })
            .OrderBy(ft => ft.FaultTypeName)
            .ToList();

        var slaMetrics = new SlaMetricsDto
        {
            OnTimeResolutionPercentage = OnTimePercentage(resolutionFacts.Select(x => (x.DueDate, x.LastSuccessEndedAt))),
            LocationBreakdown = locationBreakdown,
            FaultTypeBreakdown = faultTypeBreakdown
        };

        return Ok(slaMetrics);
    }

    private static double? AverageResolutionHours(IEnumerable<(DateTime CreatedAt, DateTime? EndedAt)> facts)
    {
        var list = facts.ToList();
        return list.Count == 0 ? null : list.Average(f => (f.EndedAt!.Value - f.CreatedAt).TotalHours);
    }

    private static double? OnTimePercentage(IEnumerable<(DateTime? DueDate, DateTime? EndedAt)> facts)
    {
        var withDueDate = facts.Where(f => f.DueDate.HasValue).ToList();
        if (withDueDate.Count == 0)
        {
            return null;
        }

        var onTimeCount = withDueDate.Count(f => f.EndedAt!.Value <= f.DueDate!.Value);
        return onTimeCount * 100.0 / withDueDate.Count;
    }

    [HttpGet("personal")]
    public async Task<ActionResult<PersonalDashboardDto>> GetPersonalDashboard()
    {
        var employeeId = User.GetEmployeeId();
        var dto = new PersonalDashboardDto();

        if (employeeId.HasValue && User.IsInRole("Reporter"))
        {
            dto.MyOpenReportsCount = await _context.FaultReports
                .CountAsync(fr => fr.ReporterId == employeeId.Value && fr.FaultStatus!.Name != StatusZatvoreno);
        }
        else if (employeeId.HasValue && User.IsInRole("Technician"))
        {
            dto.MyActiveAssignmentsCount = await _context.WorkAssignments
                .CountAsync(wa => wa.TechnicianId == employeeId.Value && wa.IsActive);
        }

        return Ok(dto);
    }
}
