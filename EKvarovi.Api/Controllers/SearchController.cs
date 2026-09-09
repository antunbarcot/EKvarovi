using EKvarovi.Api.Auth;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private const int MaxResultsPerCategory = 5;
    private const int MinQueryLength = 2;

    private readonly EKvaroviDbContext _context;

    public SearchController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<GlobalSearchResultDto>> Search([FromQuery] string? query)
    {
        var result = new GlobalSearchResultDto();

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < MinQueryLength)
        {
            return Ok(result);
        }

        var trimmedQuery = query.Trim();

        IQueryable<Shared.Models.FaultReport> faultReportsQuery = _context.FaultReports
            .Where(fr => fr.Description.Contains(trimmedQuery));

        // Reporter smije vidjeti SAMO svoje prijave u rezultatima pretrage - isto pravilo
        // kao GET api/fault-reports/mine, identitet ide iskljucivo iz JWT EmployeeId claima
        // (ne iz nekog parametra koji bi klijent mogao izmijeniti). Locations i Employees
        // ostaju vidljivi svim ulogama, kao i na njihovim redovnim GET endpointima.
        if (User.IsInRole("Reporter"))
        {
            var employeeId = User.GetEmployeeId();
            faultReportsQuery = employeeId.HasValue
                ? faultReportsQuery.Where(fr => fr.ReporterId == employeeId.Value)
                : faultReportsQuery.Where(fr => false);
        }

        // Truncate ide TEK nad vec ogranicenim (Take 5) rezultatom, u memoriji - EF Core
        // ne zna prevesti proizvoljnu C# metodu (Truncate) u SQL unutar .Select() projekcije.
        var faultReportRows = await faultReportsQuery
            .OrderByDescending(fr => fr.CreatedAt)
            .Take(MaxResultsPerCategory)
            .Select(fr => new { fr.Id, fr.Description })
            .ToListAsync();

        result.FaultReports = faultReportRows
            .Select(fr => new LookupDto { Id = fr.Id, Name = Truncate(fr.Description) })
            .ToList();

        result.Locations = await _context.Locations
            .Where(l => l.Name.Contains(trimmedQuery))
            .OrderBy(l => l.Name)
            .Take(MaxResultsPerCategory)
            .Select(l => new LookupDto { Id = l.Id, Name = l.Name })
            .ToListAsync();

        result.Employees = await _context.Employees
            .Where(e => (e.FirstName + " " + e.LastName).Contains(trimmedQuery))
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Take(MaxResultsPerCategory)
            .Select(e => new LookupDto { Id = e.Id, Name = e.FirstName + " " + e.LastName })
            .ToListAsync();

        return Ok(result);
    }

    private static string Truncate(string text, int maxLength = 60)
    {
        return text.Length > maxLength ? text[..maxLength] + "…" : text;
    }
}
