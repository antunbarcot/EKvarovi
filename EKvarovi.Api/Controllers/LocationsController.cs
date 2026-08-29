using System.Linq.Expressions;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly EKvaroviDbContext _context;

    // Expression (ne obican metoda) - EF Core je mora prevesti u SQL projekciju,
    // pa se ne moze pozvati obicna C# metoda unutar .Select() nad IQueryable.
    private static readonly Expression<Func<Location, LocationDto>> ToDtoProjection = l => new LocationDto
    {
        Id = l.Id,
        Name = l.Name,
        Address = l.Address,
        LocationTypeId = l.LocationTypeId,
        LocationTypeName = l.LocationType != null ? l.LocationType.Name : string.Empty,
        IsActive = l.IsActive,
        CreatedAt = l.CreatedAt
    };

    public LocationsController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<LocationDto>>> GetLocations()
    {
        var locations = await _context.Locations
            .OrderBy(l => l.Name)
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(locations);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LocationDto>> GetLocation(int id)
    {
        var location = await _context.Locations
            .Where(l => l.Id == id)
            .Select(ToDtoProjection)
            .FirstOrDefaultAsync();

        if (location is null)
        {
            return NotFound();
        }

        return Ok(location);
    }

    [HttpPost]
    public async Task<ActionResult<LocationDto>> CreateLocation(SaveLocationDto dto)
    {
        var locationTypeExists = await _context.LocationTypes.AnyAsync(lt => lt.Id == dto.LocationTypeId);
        if (!locationTypeExists)
        {
            return BadRequest($"LocationType s Id={dto.LocationTypeId} ne postoji.");
        }

        var location = new Location
        {
            Name = dto.Name,
            Address = dto.Address,
            LocationTypeId = dto.LocationTypeId,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Locations.Add(location);
        await _context.SaveChangesAsync();

        var createdDto = await _context.Locations
            .Where(l => l.Id == location.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetLocation), new { id = location.Id }, createdDto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateLocation(int id, SaveLocationDto dto)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (location is null)
        {
            return NotFound();
        }

        var locationTypeExists = await _context.LocationTypes.AnyAsync(lt => lt.Id == dto.LocationTypeId);
        if (!locationTypeExists)
        {
            return BadRequest($"LocationType s Id={dto.LocationTypeId} ne postoji.");
        }

        location.Name = dto.Name;
        location.Address = dto.Address;
        location.LocationTypeId = dto.LocationTypeId;
        location.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLocation(int id)
    {
        var location = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
        if (location is null)
        {
            return NotFound();
        }

        var hasFaultReports = await _context.FaultReports.AnyAsync(fr => fr.LocationId == id);
        if (hasFaultReports)
        {
            return BadRequest("Lokacija s postojećim prijavama kvarova se ne može obrisati - deaktivirajte je umjesto toga (IsActive = false).");
        }

        _context.Locations.Remove(location);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
