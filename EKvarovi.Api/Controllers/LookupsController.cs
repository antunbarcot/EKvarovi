using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupsController : ControllerBase
{
    private readonly EKvaroviDbContext _context;

    public LookupsController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet("location-types")]
    public async Task<ActionResult<List<LookupDto>>> GetLocationTypes()
    {
        var locationTypes = await _context.LocationTypes
            .OrderBy(lt => lt.Name)
            .Select(lt => new LookupDto { Id = lt.Id, Name = lt.Name })
            .ToListAsync();

        return Ok(locationTypes);
    }

    [HttpGet("locations")]
    public async Task<ActionResult<List<LookupDto>>> GetLocations()
    {
        var locations = await _context.Locations
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new LookupDto { Id = l.Id, Name = l.Name })
            .ToListAsync();

        return Ok(locations);
    }

    [HttpGet("fault-types")]
    public async Task<ActionResult<List<LookupDto>>> GetFaultTypes()
    {
        var faultTypes = await _context.FaultTypes
            .OrderBy(ft => ft.Name)
            .Select(ft => new LookupDto { Id = ft.Id, Name = ft.Name })
            .ToListAsync();

        return Ok(faultTypes);
    }

    [HttpGet("fault-priorities")]
    public async Task<ActionResult<List<LookupDto>>> GetFaultPriorities()
    {
        var faultPriorities = await _context.FaultPriorities
            .OrderBy(fp => fp.SortOrder)
            .Select(fp => new LookupDto { Id = fp.Id, Name = fp.Name })
            .ToListAsync();

        return Ok(faultPriorities);
    }

    [HttpGet("fault-statuses")]
    public async Task<ActionResult<List<LookupDto>>> GetFaultStatuses()
    {
        var faultStatuses = await _context.FaultStatuses
            .OrderBy(fs => fs.SortOrder)
            .Select(fs => new LookupDto { Id = fs.Id, Name = fs.Name })
            .ToListAsync();

        return Ok(faultStatuses);
    }

    [HttpGet("technicians")]
    public async Task<ActionResult<List<LookupDto>>> GetTechnicians()
    {
        var technicians = await _context.Employees
            .Where(e => e.IsActive && e.IsTechnician)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(e => new LookupDto { Id = e.Id, Name = e.FirstName + " " + e.LastName })
            .ToListAsync();

        return Ok(technicians);
    }

    [HttpGet("reporters")]
    public async Task<ActionResult<List<LookupDto>>> GetReporters()
    {
        var reporters = await _context.Employees
            .Where(e => e.IsActive && e.IsReporter)
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Select(e => new LookupDto { Id = e.Id, Name = e.FirstName + " " + e.LastName })
            .ToListAsync();

        return Ok(reporters);
    }

    [HttpGet("material-units")]
    public async Task<ActionResult<List<LookupDto>>> GetMaterialUnits()
    {
        var materialUnits = await _context.MaterialUnits
            .OrderBy(mu => mu.Name)
            .Select(mu => new LookupDto { Id = mu.Id, Name = mu.Name })
            .ToListAsync();

        return Ok(materialUnits);
    }
}
