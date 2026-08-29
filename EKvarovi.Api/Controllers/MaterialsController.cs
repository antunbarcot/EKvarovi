using System.Linq.Expressions;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialsController : ControllerBase
{
    private readonly EKvaroviDbContext _context;

    private static readonly Expression<Func<Material, MaterialDto>> ToDtoProjection = m => new MaterialDto
    {
        Id = m.Id,
        Name = m.Name,
        MaterialUnitId = m.MaterialUnitId,
        MaterialUnitName = m.MaterialUnit != null ? m.MaterialUnit.Name : string.Empty,
        MaterialUnitAbbreviation = m.MaterialUnit != null ? m.MaterialUnit.Abbreviation : string.Empty,
        IsActive = m.IsActive
    };

    public MaterialsController(EKvaroviDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<MaterialDto>>> GetMaterials()
    {
        var materials = await _context.Materials
            .OrderBy(m => m.Name)
            .Select(ToDtoProjection)
            .ToListAsync();

        return Ok(materials);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MaterialDto>> GetMaterial(int id)
    {
        var material = await _context.Materials
            .Where(m => m.Id == id)
            .Select(ToDtoProjection)
            .FirstOrDefaultAsync();

        if (material is null)
        {
            return NotFound();
        }

        return Ok(material);
    }

    [HttpPost]
    public async Task<ActionResult<MaterialDto>> CreateMaterial(SaveMaterialDto dto)
    {
        var materialUnitExists = await _context.MaterialUnits.AnyAsync(mu => mu.Id == dto.MaterialUnitId);
        if (!materialUnitExists)
        {
            return BadRequest($"MaterialUnit s Id={dto.MaterialUnitId} ne postoji.");
        }

        var material = new Material
        {
            Name = dto.Name,
            MaterialUnitId = dto.MaterialUnitId,
            IsActive = dto.IsActive
        };

        _context.Materials.Add(material);
        await _context.SaveChangesAsync();

        var createdDto = await _context.Materials
            .Where(m => m.Id == material.Id)
            .Select(ToDtoProjection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetMaterial), new { id = material.Id }, createdDto);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateMaterial(int id, SaveMaterialDto dto)
    {
        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == id);
        if (material is null)
        {
            return NotFound();
        }

        var materialUnitExists = await _context.MaterialUnits.AnyAsync(mu => mu.Id == dto.MaterialUnitId);
        if (!materialUnitExists)
        {
            return BadRequest($"MaterialUnit s Id={dto.MaterialUnitId} ne postoji.");
        }

        material.Name = dto.Name;
        material.MaterialUnitId = dto.MaterialUnitId;
        material.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMaterial(int id)
    {
        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == id);
        if (material is null)
        {
            return NotFound();
        }

        var isUsedInInterventions = await _context.InterventionMaterials.AnyAsync(im => im.MaterialId == id);
        if (isUsedInInterventions)
        {
            return BadRequest("Materijal je evidentiran na postojećim intervencijama i ne može se obrisati - deaktivirajte ga umjesto toga (IsActive = false).");
        }

        _context.Materials.Remove(material);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
