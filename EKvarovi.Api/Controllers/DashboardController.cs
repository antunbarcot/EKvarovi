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

        // Upiti idu redom (await jedan po jedan), ne paralelno preko Task.WhenAll - jedan
        // EKvaroviDbContext ne podrzava vise istovremenih operacija nad istom instancom.
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

        // "Pregledano ili dalje" = SortOrder usporedba (ne popis imena) - tako obuhvaca i
        // rubni slucaj gdje bi prijava vec dalje u toku (npr. Dodijeljeno) ostala bez
        // ikakve aktivne dodjele (npr. nakon rucnog uklanjanja u bazi).
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

        // Prosjecno vrijeme rjesavanja se racuna preko EndedAt zadnje USPJESNE intervencije
        // na prijavi (ne UpdatedAt), jer UpdatedAt prijave se mijenja i kod kasnijeg prijelaza
        // u "Zatvoreno" pa vise ne bi odrazavao trenutak stvarnog rjesenja kvara.
        // SQLite/EF Core ne prevodi oduzimanje DateTime vrijednosti u prevediv SQL AVG izraz,
        // pa se u bazi radi samo projekcija (po prijavi: CreatedAt + EndedAt zadnje uspjesne
        // intervencije), a sam prosjek (jednostavno dijeljenje) racuna nad tom vec agregiranom,
        // malom listom u memoriji.
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

    // SLA izvjestaj: postotak na vrijeme rijesenih prijava, s raspodjelom po lokaciji i
    // tipu kvara. TotalReports po lokaciji/tipu je prava SQL GROUP BY + COUNT(*) agregacija
    // (FaultTypeBreakdown preskace prijave bez FaultTypeId - jos nisu ni pregledane).
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

        // AverageResolutionHours i OnTimePercentage trebaju stvarno trajanje (EndedAt - CreatedAt)
        // i usporedbu s DueDate - SQLite/EF Core ne prevodi oduzimanje DateTime vrijednosti u
        // prevediv SQL AVG izraz (isto ogranicenje kao kod AverageResolutionTimeHours gore), pa
        // upit ispod u bazi radi samo filtriranje (status Rijeseno/Zatvoreno) i projekciju po
        // prijavi (LocationId/FaultTypeId/CreatedAt/DueDate/EndedAt zadnje uspjesne intervencije) -
        // stvarni agregati (prosjek, postotak po lokaciji/tipu) racunaju se nad tom vec suzenom,
        // malom listom u memoriji, ne nad cijelom tablicom FaultReports.
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

    // "Na vrijeme" racuna se samo nad prijavama koje su IMALE rok (DueDate) - prijave bez
    // roka ne mogu ni zakasniti ni stici na vrijeme, pa se izostavljaju iz nazivnika.
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

    // Identitet se cita ISKLJUCIVO iz JWT "EmployeeId" claima, isto pravilo kao i
    // ostali /mine endpointi. Admin/Manager nemaju "osobni" profil u ovom smislu pa
    // za njih oba polja ostaju null (vidi objasnjenje uz zadatak).
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
