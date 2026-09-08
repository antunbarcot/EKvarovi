using System.Text;
using EKvarovi.Api.Data;
using EKvarovi.Api.Services;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EKvarovi.Api.Controllers;

// AI funkcije su pomocni alat za upravitelje (sazetak naloga, prijedlog tipa/prioriteta),
// ne za sve uloge - Technician/Reporter ovamo nemaju pristup.
// KRITICNO: ovaj kontroler NIKAD ne sprema nista u FaultReports - fault-report-suggestion
// samo VRACA prijedlog, korisnik ga potvrduje/odbacuje kroz postojeci
// PUT api/fault-reports/{id}/review.
[ApiController]
[Route("api/ai")]
[Authorize(Roles = "Admin,Manager")]
public class AiController : ControllerBase
{
    private readonly EKvaroviDbContext _context;
    private readonly IAiService _aiService;

    public AiController(EKvaroviDbContext context, IAiService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    [HttpGet("work-order-summary/{workAssignmentId:int}")]
    public async Task<ActionResult<AiTextResponseDto>> GetWorkOrderSummary(int workAssignmentId)
    {
        var workAssignment = await _context.WorkAssignments
            .Include(wa => wa.FaultReport)
            .Include(wa => wa.Technician)
            .Include(wa => wa.Interventions)
                .ThenInclude(i => i.InterventionStatus)
            .Include(wa => wa.Interventions)
                .ThenInclude(i => i.InterventionMaterials)
                    .ThenInclude(im => im.Material)
                        .ThenInclude(m => m!.MaterialUnit)
            .FirstOrDefaultAsync(wa => wa.Id == workAssignmentId);

        if (workAssignment is null)
        {
            return NotFound();
        }

        var prompt = BuildWorkOrderSummaryPrompt(workAssignment);
        var summary = await _aiService.GenerateTextAsync(prompt);

        return Ok(new AiTextResponseDto { Summary = summary });
    }

    // Ovaj endpoint SAMO vraca prijedlog - ne dira bazu. Korisnik prihvaca/odbacuje
    // prijedlog i tek onda salje potvrdene vrijednosti kroz PUT .../review.
    [HttpPost("fault-report-suggestion")]
    public async Task<ActionResult<FaultReportSuggestionDto>> GetFaultReportSuggestion(FaultReportSuggestionRequestDto dto)
    {
        var suggestion = await _aiService.GenerateStructuredAsync<FaultReportSuggestionDto>(dto.Description.Trim());

        if (suggestion is null)
        {
            return Ok(new FaultReportSuggestionDto
            {
                Reasoning = "Nije moguće generirati prijedlog."
            });
        }

        return Ok(suggestion);
    }

    private static string BuildWorkOrderSummaryPrompt(WorkAssignment workAssignment)
    {
        var interventions = workAssignment.Interventions
            .OrderBy(i => i.StartedAt)
            .ToList();

        var successful = interventions.Count(i => i.InterventionStatus?.Name == "Završena");
        var failed = interventions.Count(i => i.InterventionStatus?.Name == "Neuspješna");
        var inProgress = interventions.Count(i => i.InterventionStatus?.Name == "U tijeku");
        var totalDuration = interventions.Sum(i => i.DurationMinutes ?? 0);

        var sb = new StringBuilder();
        sb.AppendLine("Sažetak radnog naloga.");
        sb.AppendLine($"Opis prijave: {workAssignment.FaultReport?.Description}");
        sb.AppendLine($"Izvršitelj: {workAssignment.Technician?.FirstName} {workAssignment.Technician?.LastName}");
        sb.AppendLine($"Broj intervencija: {interventions.Count}");
        sb.AppendLine($"Uspješne: {successful}");
        sb.AppendLine($"Neuspješne: {failed}");
        sb.AppendLine($"U tijeku: {inProgress}");
        sb.AppendLine($"Ukupno trajanje (min): {totalDuration}");
        sb.AppendLine();
        sb.AppendLine("Detalji intervencija:");

        foreach (var intervention in interventions)
        {
            sb.AppendLine(
                $"- Intervencija #{intervention.Id}, status: {intervention.InterventionStatus?.Name}, " +
                $"početak: {intervention.StartedAt:dd.MM.yyyy HH:mm}, kraj: {(intervention.EndedAt is null ? "-" : intervention.EndedAt.Value.ToString("dd.MM.yyyy HH:mm"))}, " +
                $"trajanje: {intervention.DurationMinutes?.ToString() ?? "-"} min");

            if (!string.IsNullOrWhiteSpace(intervention.Notes))
            {
                sb.AppendLine($"  Bilješka: {intervention.Notes}");
            }

            foreach (var material in intervention.InterventionMaterials)
            {
                sb.AppendLine($"  Materijal: {material.Material?.Name} {material.Quantity} {material.Material?.MaterialUnit?.Abbreviation}");
            }
        }

        return sb.ToString();
    }
}
